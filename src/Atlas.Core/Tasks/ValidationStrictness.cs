namespace Atlas.Core.Tasks;

/// <summary>
/// How rigorously a task's outputs must be validated before use (arch §20, §24).
/// </summary>
/// <remarks>
/// Validation is a default, not an afterthought: "important outputs must not
/// pass directly from model to user or model to app state without at least a
/// schema check and scope check" (arch §20). This value selects how much of the
/// validation ladder runs. Deterministic checks come first and are cheap; model
/// validators are reserved for quality judgements (arch §29).
/// </remarks>
public enum ValidationStrictness
{
    /// <summary>
    /// No validation. Reserved for trivial, low-risk output such as autocomplete
    /// where the user is the immediate validator.
    /// </summary>
    None = 0,

    /// <summary>
    /// Deterministic schema validation only: does the output parse and match the
    /// expected structure?
    /// </summary>
    SchemaOnly = 1,

    /// <summary>
    /// Schema plus scope, citation, and source-alignment checks — the standard
    /// bar for content that touches notes, files, or app state.
    /// </summary>
    Full = 2,

    /// <summary>
    /// Everything in <see cref="Full"/> plus model-assisted quality scoring,
    /// duplicate detection, and consistency checks. Used for the most complex
    /// generation (e.g. learning paths).
    /// </summary>
    Paranoid = 3,
}
