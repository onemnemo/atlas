using System.Runtime.CompilerServices;

namespace Atlas.Core.Inference;

/// <summary>
/// The transport to the local model server (arch §31.3).
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally the narrowest possible abstraction over inference: send
/// a fully-resolved <see cref="InferenceRequest"/>, await an
/// <see cref="InferenceResponse"/>. It hides whether the backend is llama.cpp in
/// router mode, a single hand-managed server, or a fake used in tests. Swapping
/// the inference engine is implementing this one interface (arch §31.2).
/// </para>
/// <para>
/// Implementations should surface backend failures as an
/// <see cref="InferenceResponse"/> with <see cref="FinishReason.Error"/> (or
/// <see cref="FinishReason.Cancelled"/>) where they reasonably can, so the
/// pipeline can degrade rather than unwind. Throwing is reserved for unexpected
/// transport faults the caller cannot anticipate.
/// </para>
/// </remarks>
public interface IInferenceClient
{
    /// <summary>
    /// Sends <paramref name="request"/> to the backend and returns its response.
    /// </summary>
    /// <param name="request">The fully-resolved inference request.</param>
    /// <param name="cancellationToken">Cancels the call (e.g. on a latency-gate timeout).</param>
    /// <returns>The model's response, including how generation finished.</returns>
    Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends <paramref name="request"/> to the backend and streams back partial
    /// tokens as they are generated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default implementation wraps <see cref="CompleteAsync"/> and emits the
    /// full response text as a single chunk — correct but not streaming.
    /// Override this method in production clients to use SSE streaming so the UI
    /// can display tokens progressively (arch §31.3).
    /// </para>
    /// <para>
    /// The caller is responsible for assembling the final text from the emitted
    /// chunks.  The stream ends naturally when generation finishes or
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </para>
    /// </remarks>
    /// <param name="request">The fully-resolved inference request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>An async sequence of partial token strings.</returns>
    async IAsyncEnumerable<string> CompleteStreamingAsync(
        InferenceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        InferenceResponse response = await CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(response.Text))
        {
            yield return response.Text;
        }
    }

    /// <summary>
    /// Sends <paramref name="request"/> with <paramref name="toolDefinitions"/> exposed to
    /// the model, then returns either a tool-call batch (the model wants to invoke tools) or
    /// a final text response.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass all completed rounds from previous iterations of the tool-call loop in
    /// <paramref name="previousRounds"/> so the model can see what it asked for and what
    /// each tool returned. The method appends the round history to the conversation before
    /// calling the backend.
    /// </para>
    /// <para>
    /// The default implementation ignores tools and wraps <see cref="CompleteAsync"/> in a
    /// <see cref="ToolAugmentedResponse.Text"/> result — correct fallback for backends that
    /// do not support function calling, and for test fakes (arch §31.2).
    /// </para>
    /// </remarks>
    /// <param name="request">Initial request carrying the system + user messages and model settings.</param>
    /// <param name="toolDefinitions">The tools to offer to the model for this call.</param>
    /// <param name="previousRounds">
    /// Completed tool-call rounds to inject as conversation history before calling the model.
    /// Pass null or empty on the first call.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    async Task<ToolAugmentedResponse> CompleteWithToolsAsync(
        InferenceRequest request,
        IReadOnlyList<ToolDefinition> toolDefinitions,
        IReadOnlyList<ToolRoundResult>? previousRounds = null,
        CancellationToken cancellationToken = default)
    {
        // Default: ignore tools and previous rounds, return the plain completion.
        InferenceResponse response = await CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        return ToolAugmentedResponse.Text(response);
    }
}
