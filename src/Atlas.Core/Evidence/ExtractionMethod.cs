namespace Atlas.Core.Evidence;

/// <summary>
/// How a piece of information was located (arch §18).
/// </summary>
/// <remarks>
/// The extraction method is not bookkeeping — it determines the trust weight a
/// finding carries in validation. "A result found by exact match carries
/// different confidence than a result found by semantic similarity" (arch §18).
/// Members are ordered from most to least intrinsically trustworthy.
/// </remarks>
public enum ExtractionMethod
{
    /// <summary>An exact, verbatim match against the source text.</summary>
    ExactMatch = 0,

    /// <summary>A keyword/lexical match.</summary>
    Keyword = 1,

    /// <summary>An embedding-similarity (semantic) match.</summary>
    Semantic = 2,

    /// <summary>Reached by traversing a mindmap / learning-path / concept graph.</summary>
    GraphTraversal = 3,

    /// <summary>
    /// Produced by summarising a source. The most lossy method — a summarised
    /// finding should be treated with the most caution and, where the budget
    /// allows, replaced by the underlying chunk (arch §26).
    /// </summary>
    Summarized = 4,
}
