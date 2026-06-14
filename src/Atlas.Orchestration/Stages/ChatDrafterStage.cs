using Atlas.Core.Contracts;
using Atlas.Core.Diagnostics;
using Atlas.Core.Inference;
using Atlas.Core.Pipeline;
using Microsoft.Extensions.Options;

namespace Atlas.Orchestration.Stages;

/// <summary>
/// The model-backed stage that drafts a chat reply (arch §6, §16).
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately narrow node: it builds the smallest sufficient message
/// set, resolves the main-worker model for the current hardware, calls the
/// inference backend, and maps the raw response into a typed
/// <see cref="StageOutcome{T}"/>. It owns no retry or routing logic — that is the
/// route's job — which keeps it a clean replacement target for a fine-tuned chat
/// model later (arch §16).
/// </para>
/// <para>
/// It treats the model as untrusted: an errored, empty, or cancelled response
/// becomes a failure, and a truncated (length-capped) response becomes a degraded
/// outcome with a warning rather than being passed off as complete (arch §26).
/// </para>
/// </remarks>
public sealed class ChatDrafterStage : IPipelineStage<ChatDraftInput, string>
{
    private readonly IModelResolver _modelResolver;
    private readonly IInferenceClient _inferenceClient;
    private readonly IOptions<ChatOptions> _chatOptions;

    /// <summary>Creates the stage.</summary>
    public ChatDrafterStage(
        IModelResolver modelResolver,
        IInferenceClient inferenceClient,
        IOptions<ChatOptions> chatOptions)
    {
        ArgumentNullException.ThrowIfNull(modelResolver);
        ArgumentNullException.ThrowIfNull(inferenceClient);
        ArgumentNullException.ThrowIfNull(chatOptions);
        _modelResolver = modelResolver;
        _inferenceClient = inferenceClient;
        _chatOptions = chatOptions;
    }

    /// <inheritdoc />
    public StageDescriptor Descriptor { get; } = new(
        StageId: "chat.drafter",
        Version: "1.0",
        OutputContract: OutputContract.Text("chat.reply.v1", validationEntrypoint: "chat.reply.deterministic"),
        ModelRole: ModelRole.MainWorker);

    /// <inheritdoc />
    public async ValueTask<StageOutcome<string>> ExecuteAsync(
        StageContext context,
        ChatDraftInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        ModelDescriptor model = input.ModelOverride
            ?? _modelResolver.Resolve(ModelRole.MainWorker, context.Hardware);

        int maxTokens = input.MaxOutputTokensOverride ?? Math.Max(1, context.Budget.GenerationTokens);

        var request = new InferenceRequest(
            Model: model,
            Messages:
            [
                InferenceMessage.System(input.SystemPrompt),
                InferenceMessage.User(input.UserInput),
            ],
            MaxOutputTokens: maxTokens,
            Temperature: _chatOptions.Value.Temperature);

        InferenceResponse response = await _inferenceClient
            .CompleteAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return MapResponse(response);
    }

    private static StageOutcome<string> MapResponse(InferenceResponse response)
    {
        switch (response.FinishReason)
        {
            case FinishReason.Cancelled:
                return StageOutcome.Failed<string>(
                    AtlasWarning.Error(FailureMode.ModelCallTimeout, "The model call did not complete in time."));

            case FinishReason.Error:
                return StageOutcome.Failed<string>(
                    AtlasWarning.Error(FailureMode.MalformedOutput, "The inference backend returned an error."));

            case FinishReason.ContentFilter:
                return StageOutcome.Failed<string>(
                    AtlasWarning.Error(FailureMode.MalformedOutput, "The response was filtered by the backend."));
        }

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            return StageOutcome.Failed<string>(
                AtlasWarning.Error(FailureMode.MalformedOutput, "The model produced an empty response."));
        }

        if (response.FinishReason == FinishReason.Length)
        {
            return StageOutcome.Degraded(
                response.Text,
                AtlasWarning.Caution(
                    FailureMode.ContextOverflow,
                    "The reply was cut off at the length limit and may be incomplete."));
        }

        return StageOutcome.Success(response.Text);
    }
}
