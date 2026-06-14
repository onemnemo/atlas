namespace Atlas.Core.Tasks;

/// <summary>
/// The acceptable response-time class for a task (arch §24).
/// </summary>
/// <remarks>
/// The latency target is an input to execution-strategy decisions (how much
/// retrieval, how many validation passes, whether to escalate to a larger
/// model) and to the latency gate that produces a partial result on timeout
/// (arch §26). It is a budget, not a guarantee.
/// </remarks>
public enum LatencyTarget
{
    /// <summary>Interactive, sub-second feel (e.g. autocomplete, inline rewrite).</summary>
    Fast = 0,

    /// <summary>Conversational responsiveness (e.g. chat, mindmap edits).</summary>
    Normal = 1,

    /// <summary>The user expects to wait (e.g. learning-path generation).</summary>
    Slow = 2,

    /// <summary>Runs off the interactive path entirely (e.g. file ingestion).</summary>
    Background = 3,
}
