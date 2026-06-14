namespace Atlas.Core.Inference;

/// <summary>
/// A logical model role — the capability a stage needs, independent of which
/// concrete model fills it (arch §16, model sheet).
/// </summary>
/// <remarks>
/// <para>
/// Stages and tasks reference a role, never a model file. The
/// <see cref="IModelResolver"/> maps a role plus the current hardware to a
/// concrete model. This indirection is the architecture's central evolution
/// path (arch §16):
/// </para>
/// <list type="number">
///   <item>v1 — a prompted general model fills the role;</item>
///   <item>v2 — a fine-tuned model replaces the prompted one for that role;</item>
///   <item>v3 — a specialist model per branch, trained on mnemo data.</item>
/// </list>
/// <para>
/// Because the role and its input/output contract stay fixed, swapping the model
/// behind a role touches only the resolver's configuration — never the pipeline.
/// </para>
/// </remarks>
public enum ModelRole
{
    /// <summary>
    /// Maps a user request to a capability branch. Classification, not
    /// generation — the output is a branch label (arch §16). The tiny model in
    /// the model sheet (e.g. Qwen3 0.6B).
    /// </summary>
    Router = 0,

    /// <summary>Decides which retrieval sources to use — classification over a fixed source list (arch §16).</summary>
    RetrievalRouter = 1,

    /// <summary>Produces clean, structured tool-call arguments under a narrow schema (arch §16).</summary>
    ToolArgumentGenerator = 2,

    /// <summary>Fast, low-latency worker for cheap generation and rewriting (model sheet fast worker).</summary>
    FastWorker = 3,

    /// <summary>The primary drafting worker for most generation (model sheet main worker).</summary>
    MainWorker = 4,

    /// <summary>
    /// Larger fallback model, used only when a smaller model fails validation
    /// repeatedly (arch §21, model sheet fallback).
    /// </summary>
    Fallback = 5,

    /// <summary>Checks edit scope, ids, and output format — binary + reason, no generation (arch §16).</summary>
    Validator = 6,

    /// <summary>Compresses conversation history into a rolling summary (arch §16).</summary>
    Summarizer = 7,

    /// <summary>Inspects block structure and proposes safe block-level edits (arch §16).</summary>
    EditPlanner = 8,

    /// <summary>Explains what changed in user-friendly language from a fixed template (arch §16).</summary>
    DiffExplainer = 9,
}
