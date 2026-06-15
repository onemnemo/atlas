namespace Atlas.Core.Diagnostics;

/// <summary>
/// The catalogue of anticipated failure modes, each with a defined degradation
/// contract (arch §26).
/// </summary>
/// <remarks>
/// <para>
/// These are <em>expected</em> failures of small, unreliable models and
/// best-effort retrieval — not bugs. Every one has a defined degradation
/// response in arch §26; tagging a <see cref="AtlasWarning"/> with the mode lets
/// the orchestrator and UI react consistently and lets telemetry track which
/// failure modes actually occur in the field.
/// </para>
/// <para>
/// "A degraded result with explicit warnings is always better than a silent
/// failure or a confident hallucination" (arch §26).
/// </para>
/// </remarks>
public enum FailureMode
{
    /// <summary>No specific failure mode; a general-purpose note.</summary>
    None = 0,

    /// <summary>Model produced output that does not parse / match its schema.</summary>
    MalformedOutput = 1,

    /// <summary>A claim not supported by any retrieved source.</summary>
    HallucinatedClaim = 2,

    /// <summary>A referenced block/node/chunk id does not resolve.</summary>
    BrokenReference = 3,

    /// <summary>Assembled context exceeded the token budget and was trimmed.</summary>
    ContextOverflow = 4,

    /// <summary>Retrieval found no relevant content.</summary>
    RetrievalEmpty = 5,

    /// <summary>The bounded repair loop was exhausted without a clean result.</summary>
    RepairLoopExhausted = 6,

    /// <summary>A model call exceeded its latency budget.</summary>
    ModelCallTimeout = 7,

    /// <summary>An action was blocked because the required permission was not granted.</summary>
    PermissionDenied = 8,

    /// <summary>A subagent failed to return a usable evidence packet.</summary>
    SubagentFailure = 9,

    /// <summary>A summary failed quality checks; the original chunk is preferred.</summary>
    BadSummarization = 10,

    /// <summary>
    /// A required external resource (a tool backend, MCP server, or network
    /// endpoint) was unavailable or not configured.
    /// </summary>
    ResourceUnavailable = 11,
}
