using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
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
        ChatCompletionRequest payload = BuildPayload(request, stream: false);

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

    /// <inheritdoc />
    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        InferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        string endpoint = CombineUrl(_options.ResolveEndpoint(request.Model.Name), _options.ChatCompletionsPath);
        ChatCompletionRequest payload = BuildPayload(request, stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions),
        };

        HttpResponseMessage? httpResponse = null;
        try
        {
            httpResponse = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogBackendError(_logger, request.Model.Name, 0, ex);
            yield break;
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            LogBackendError(_logger, request.Model.Name, (int)httpResponse.StatusCode, null);
            httpResponse.Dispose();
            yield break;
        }

        using (httpResponse)
        {
            System.IO.Stream contentStream;
            try
            {
                contentStream = await httpResponse.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogBackendError(_logger, request.Model.Name, 0, ex);
                yield break;
            }

            using var reader = new System.IO.StreamReader(contentStream);
            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    yield break;
                }

                if (line is null)
                {
                    break;
                }

                // SSE lines are prefixed "data: "; empty lines are separators.
                if (!line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    continue;
                }

                ReadOnlySpan<char> data = line.AsSpan("data: ".Length);
                if (data.SequenceEqual("[DONE]"))
                {
                    break;
                }

                ChatCompletionStreamChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(data, SerializerOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                string? token = chunk?.Choices is { Count: > 0 } c ? c[0].Delta?.Content : null;
                if (!string.IsNullOrEmpty(token))
                {
                    yield return token;
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<ToolAugmentedResponse> CompleteWithToolsAsync(
        InferenceRequest request,
        IReadOnlyList<ToolDefinition> toolDefinitions,
        IReadOnlyList<ToolRoundResult>? previousRounds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(toolDefinitions);
        request.Validate();

        string endpoint = CombineUrl(_options.ResolveEndpoint(request.Model.Name), _options.ChatCompletionsPath);
        ChatCompletionRequest payload = BuildToolPayload(request, toolDefinitions, previousRounds);

        try
        {
            using HttpResponseMessage httpResponse = await _httpClient
                .PostAsJsonAsync(endpoint, payload, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                LogBackendError(_logger, request.Model.Name, (int)httpResponse.StatusCode, null);
                return ToolAugmentedResponse.Text(ErrorResponse(request.Model.Name,
                    $"Backend returned HTTP {(int)httpResponse.StatusCode}."));
            }

            ChatCompletionResponse? body = await httpResponse.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            ChoiceDto? choice = body?.Choices is { Count: > 0 } c ? c[0] : null;
            if (choice is null)
            {
                return ToolAugmentedResponse.Text(ErrorResponse(request.Model.Name, "Backend returned no choices."));
            }

            // If the model wants to call tools, return the call list.
            if (choice.IsToolCall && choice.Message?.ToolCalls is { Count: > 0 } toolCalls)
            {
                var requests = new List<ToolCallRequest>(toolCalls.Count);
                foreach (ToolCallDto call in toolCalls)
                {
                    requests.Add(new ToolCallRequest(
                        CallId: call.Id,
                        ToolName: call.Function?.Name ?? string.Empty,
                        ArgumentsJson: call.Function?.Arguments ?? "{}"));
                }

                return ToolAugmentedResponse.ToolCalling(requests);
            }

            return ToolAugmentedResponse.Text(MapResponse(request.Model.Name, body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimeout(_logger, request.Model.Name, _options.RequestTimeoutSeconds);
            return ToolAugmentedResponse.Text(
                new InferenceResponse(string.Empty, FinishReason.Cancelled, default, request.Model.Name));
        }
        catch (HttpRequestException ex)
        {
            LogBackendError(_logger, request.Model.Name, 0, ex);
            return ToolAugmentedResponse.Text(ErrorResponse(request.Model.Name, ex.Message));
        }
        catch (JsonException ex)
        {
            LogBackendError(_logger, request.Model.Name, 0, ex);
            return ToolAugmentedResponse.Text(ErrorResponse(request.Model.Name, "Backend returned an unparseable response."));
        }
    }

    private ChatCompletionRequest BuildToolPayload(
        InferenceRequest request,
        IReadOnlyList<ToolDefinition> toolDefinitions,
        IReadOnlyList<ToolRoundResult>? previousRounds)
    {
        var messages = BuildToolMessages(request, previousRounds);

        IReadOnlyList<ToolDefinitionDto>? toolDtos = null;
        if (toolDefinitions.Count > 0)
        {
            var dtos = new List<ToolDefinitionDto>(toolDefinitions.Count);
            foreach (ToolDefinition def in toolDefinitions)
            {
                JsonNode? schema = null;
                if (!string.IsNullOrWhiteSpace(def.ParametersSchemaJson))
                {
                    try { schema = JsonNode.Parse(def.ParametersSchemaJson); }
                    catch (JsonException) { /* malformed schema: expose no parameters */ }
                }

                dtos.Add(new ToolDefinitionDto
                {
                    Function = new ToolFunctionDto
                    {
                        Name = def.Name,
                        Description = def.Description,
                        Parameters = schema,
                    },
                });
            }

            toolDtos = dtos;
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
            Tools = toolDtos,
            Stream = false,
        };
    }

    private static List<ChatMessageDto> BuildToolMessages(
        InferenceRequest request,
        IReadOnlyList<ToolRoundResult>? previousRounds)
    {
        var messages = new List<ChatMessageDto>(request.Messages.Length + (previousRounds?.Count ?? 0) * 4);

        // Seed with the initial request messages (system + user).
        foreach (InferenceMessage message in request.Messages)
        {
            messages.Add(new ChatMessageDto { Role = ToWireRole(message.Role), Content = message.Content });
        }

        // Append completed rounds: assistant-with-tool-calls then tool results.
        if (previousRounds is null)
        {
            return messages;
        }

        foreach (ToolRoundResult round in previousRounds)
        {
            // The assistant message that triggered the round.
            var toolCallDtos = new List<ToolCallDto>(round.Calls.Count);
            foreach (ToolCallRequest call in round.Calls)
            {
                toolCallDtos.Add(new ToolCallDto
                {
                    Id = call.CallId,
                    Function = new ToolCallFunctionDto
                    {
                        Name = call.ToolName,
                        Arguments = call.ArgumentsJson,
                    },
                });
            }

            messages.Add(new ChatMessageDto
            {
                Role = "assistant",
                Content = null,
                ToolCalls = toolCallDtos,
            });

            // One result message per call, correlated by CallId.
            for (int i = 0; i < round.Calls.Count; i++)
            {
                messages.Add(new ChatMessageDto
                {
                    Role = "tool",
                    ToolCallId = round.Calls[i].CallId,
                    Content = round.Results[i],
                });
            }
        }

        return messages;
    }

    private ChatCompletionRequest BuildPayload(InferenceRequest request, bool stream)
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
            Stream = stream,
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
            // Content is nullable in the DTO (null for tool-call assistant messages).
            Text: choice.Message.Content ?? string.Empty,
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
