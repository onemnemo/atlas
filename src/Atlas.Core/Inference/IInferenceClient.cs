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
}
