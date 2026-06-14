using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Core.Inference;
using Atlas.Inference.Configuration;
using Atlas.Inference.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Inference;

/// <summary>
/// An <see cref="IInferenceClient"/> that talks to a local OpenAI-compatible
/// endpoint — llama.cpp's <c>llama-server</c>, in router mode or as plain
/// per-model servers (arch §31.3).
/// </summary>
/// <remarks>
/// <para>
/// The client is a thin transport. It maps a resolved
/// <see cref="InferenceRequest"/> onto a chat-completions POST, sends it to the
/// endpoint that serves the requested model, and maps the response back. It
/// holds no pipeline or task knowledge.
/// </para>
/// <para>
/// Following the inference contract, anticipated backend faults (a non-success
/// status, a transport error, or the client's own timeout) are returned as an
/// <see cref="InferenceResponse"/> with <see cref="FinishReason.Error"/> or
/// <see cref="FinishReason.Cancelled"/> so the pipeline can degrade rather than
/// unwind. Only genuine caller-initiated cancellation propagates as an exception.
/// </para>
/// </remarks>
public sealed partial class OpenAiCompatibleInferenceClient : IInferenceClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly InferenceOptions _options;
    private readonly ILogger<OpenAiCompatibleInferenceClient> _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">The HTTP client used for requests.</param>
    /// <param name="options">Inference options (endpoints, paths, timeout).</param>
    /// <param name="logger">Optional logger.</param>
    public OpenAiCompatibleInferenceClient(
        HttpClient httpClient,
        IOptions<InferenceOptions> options,
        ILogger<OpenAiCompatibleInferenceClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiCompatibleInferenceClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<InferenceResponse> CompleteAsync(
        InferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        string endpoint = CombineUrl(_options.ResolveEndpoint(request.Model.Name), _options.ChatCompletionsPath);
        ChatCompletionRequest payload = BuildPayload(request);

        try
        {
            using HttpResponseMessage httpResponse = await _httpClient
                .PostAsJsonAsync(endpoint, payload, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                LogBackendError(_logger, request.Model.Name, (int)httpResponse.StatusCode, null);
                return ErrorResponse(request.Model.Name, $"Backend returned HTTP {(int)httpResponse.StatusCode}.");
            }

            ChatCompletionResponse? body = await httpResponse.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return MapResponse(request.Model.Name, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine caller cancellation: let it propagate.
            throw;
        }
        catch (OperationCanceledException)
        {
            // The client's own timeout fired (not the caller's token).
            LogTimeout(_logger, request.Model.Name, _options.RequestTimeoutSeconds);
            return new InferenceResponse(string.Empty, FinishReason.Cancelled, default, request.Model.Name);
        }
        catch (HttpRequestException ex)
        {
            LogBackendError(_logger, request.Model.Name, 0, ex);
            return ErrorResponse(request.Model.Name, ex.Message);
        }
        catch (JsonException ex)
        {
            // The backend returned a body we could not parse — degrade rather
            // than unwind, consistent with the inference contract (arch §26).
            LogBackendError(_logger, request.Model.Name, 0, ex);
            return ErrorResponse(request.Model.Name, "Backend returned an unparseable response.");
        }
    }

    private ChatCompletionRequest BuildPayload(InferenceRequest request)
    {
        var messages = new List<ChatMessageDto>(request.Messages.Length);
        foreach (InferenceMessage message in request.Messages)
        {
            messages.Add(new ChatMessageDto { Role = ToWireRole(message.Role), Content = message.Content });
        }

        IReadOnlyList<string>? stop = request.StopSequences.IsDefaultOrEmpty
            ? null
            : request.StopSequences;

        return new ChatCompletionRequest
        {
            Model = request.Model.Name,
            Messages = messages,
            MaxTokens = request.MaxOutputTokens,
            Temperature = request.Temperature,
            Stop = stop,
            ResponseFormat = BuildResponseFormat(request),
            Stream = false,
        };
    }

    private ResponseFormatDto? BuildResponseFormat(InferenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JsonSchema) || !request.Model.SupportsStructuredOutput)
        {
            return null;
        }

        try
        {
            JsonNode? schema = JsonNode.Parse(request.JsonSchema);
            if (schema is null)
            {
                return null;
            }

            return new ResponseFormatDto
            {
                Type = "json_schema",
                JsonSchema = new JsonSchemaDto { Name = "atlas_output", Schema = schema },
            };
        }
        catch (JsonException)
        {
            // A malformed schema is a caller bug, but the safe runtime behaviour
            // is to fall back to unconstrained decoding plus downstream validation
            // rather than failing the call.
            LogBadSchema(_logger, request.Model.Name);
            return null;
        }
    }

    private static InferenceResponse MapResponse(string requestedModel, ChatCompletionResponse? body)
    {
        ChoiceDto? choice = body?.Choices is { Count: > 0 } choices ? choices[0] : null;
        if (choice?.Message is null)
        {
            return new InferenceResponse(string.Empty, FinishReason.Error, default, requestedModel);
        }

        var usage = new TokenUsage(body?.Usage?.PromptTokens ?? 0, body?.Usage?.CompletionTokens ?? 0);
        return new InferenceResponse(
            Text: choice.Message.Content,
            FinishReason: ParseFinishReason(choice.FinishReason),
            Usage: usage,
            ModelName: body?.Model ?? requestedModel);
    }

    private static InferenceResponse ErrorResponse(string model, string detail) =>
        new(detail, FinishReason.Error, default, model);

    private static FinishReason ParseFinishReason(string? reason) => reason switch
    {
        "stop" => FinishReason.Stop,
        "length" => FinishReason.Length,
        "content_filter" => FinishReason.ContentFilter,
        _ => FinishReason.Stop,
    };

    private static string ToWireRole(MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        _ => "user",
    };

    private static string CombineUrl(string baseUrl, string path) =>
        $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inference backend error for model {Model} (status {Status}).")]
    private static partial void LogBackendError(ILogger logger, string model, int status, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Inference request for model {Model} timed out after {TimeoutSeconds}s.")]
    private static partial void LogTimeout(ILogger logger, string model, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring malformed JSON schema for model {Model}; falling back to unconstrained decoding.")]
    private static partial void LogBadSchema(ILogger logger, string model);
}
