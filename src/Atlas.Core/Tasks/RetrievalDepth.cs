namespace Atlas.Core.Tasks;

/// <summary>
/// How far the retrieval cascade is permitted to go for a task (arch §7, §24).
/// </summary>
/// <remarks>
/// Retrieval is "the cheapest path that produces sufficient confidence, not the
/// most thorough path by default" (arch §7). This value is the ceiling on that
/// cascade: it bounds how many sources are consulted, how many chunks are
/// pulled, and how many hops a graph traversal may take. It does not force
/// retrieval to occur — the cascade still stops early once confidence is met.
/// </remarks>
public enum RetrievalDepth
{
    /// <summary>No retrieval. The task runs on its direct inputs only.</summary>
    None = 0,

    /// <summary>
    /// Cheap sources only: active document, immediate context, rolling summary,
    /// and keyword index. No embedding search.
    /// </summary>
    Shallow = 1,

    /// <summary>
    /// Adds semantic search and ranked file/PDF chunks on top of the shallow
    /// sources, with a moderate chunk budget.
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Full cascade including graph traversal across mindmaps / learning paths
    /// and (when permitted) gated external sources.
    /// </summary>
    Deep = 3,
}
