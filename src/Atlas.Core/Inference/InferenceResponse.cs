namespace Atlas.Core.Inference;

/// <summary>
/// Why the model stopped generating.
/// </summary>
/// <remarks>
/// <see cref="Length"/> is a signal that the output is likely truncated and may
/// be malformed — the validator and repair loop treat it as a degradation
/// trigger rather than a clean stop (arch §26).
/// </remarks>
public enum FinishReason
{
    /// <summary>The model emitted a natural stop or a configured stop sequence.</summary>
    Stop = 0,

    /// <summary>Generation hit the token ceiling and was cut off (likely truncated).</summary>
    Length = 1,

    /// <summary>The backend filtered the content.</summary>
    ContentFilter = 2,

    /// <summary>The call was cancelled (e.g. by a latency gate) before completing.</summary>
    Cancelled = 3,

    /// <summary>The backend reported an error.</summary>
    Error = 4,
}

/// <summary>
/// Token accounting for an inference call, used to reconcile against the
/// context budget (arch §8).
/// </summary>
/// <param name="PromptTokens">Tokens consumed by the prompt.</param>
/// <param name="CompletionTokens">Tokens generated in the completion.</param>
public readonly record struct TokenUsage(int PromptTokens, int CompletionTokens)
{
    /// <summary>Total tokens billed for the call.</summary>
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// The result of an inference call (arch §31.3).
/// </summary>
/// <remarks>
/// The response reports which concrete model actually served the request in
/// <see cref="ModelName"/>, so escalation behaviour and telemetry can be
/// attributed correctly when the router substitutes or the resolver escalates.
/// </remarks>
/// <param name="Text">The generated text.</param>
/// <param name="FinishReason">Why generation stopped.</param>
/// <param name="Usage">Token accounting for the call.</param>
/// <param name="ModelName">The concrete model that served the request.</param>
public sealed record InferenceResponse(
    string Text,
    FinishReason FinishReason,
    TokenUsage Usage,
    string ModelName)
{
    /// <summary>Whether the model stopped cleanly (not truncated, filtered, cancelled, or errored).</summary>
    public bool StoppedCleanly => FinishReason == FinishReason.Stop;
}
