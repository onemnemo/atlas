namespace Atlas.Core.Hardware;

/// <summary>
/// The coarse hardware capability classes Atlas adapts to (arch §2, §23).
/// </summary>
/// <remarks>
/// <para>
/// The pipeline graph is identical across all tiers — only the execution
/// strategy changes (model size, parallelism, retrieval depth, validation
/// strictness, retry budgets). Code should branch on the tier to select
/// <em>parameters</em>, never to select a different pipeline shape. Treating the
/// tier as anything more than an execution-strategy knob reintroduces the
/// "hardware fragmentation" risk called out in arch §29.
/// </para>
/// <para>
/// The ordering of the members is meaningful: a higher value denotes strictly
/// more capability. This lets callers write comparisons such as
/// <c>tier &gt;= HardwareTier.MidRange</c>.
/// </para>
/// </remarks>
public enum HardwareTier
{
    /// <summary>
    /// Weak machines: 1–3B quantized models, serial execution only, shallow
    /// keyword-first retrieval, deterministic-only validation. The UI must stay
    /// responsive, so no concurrent inference is allowed (arch §17).
    /// </summary>
    LowEnd = 0,

    /// <summary>
    /// Typical machines: 3–7B instruct models plus embeddings, limited
    /// parallelism (2 subagents max), semantic retrieval with a reranker, and a
    /// light model validation pass.
    /// </summary>
    MidRange = 1,

    /// <summary>
    /// Capable machines: 7B+ models (optionally several), parallel subagents,
    /// deep RAG with graph traversal, and the full validation loop.
    /// </summary>
    HighEnd = 2,
}
