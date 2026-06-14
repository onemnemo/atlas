namespace Atlas.Core.Contracts;

/// <summary>
/// The surface form a stage's output takes (arch §25).
/// </summary>
/// <remarks>
/// The format selects which deterministic parser/validator runs first. For
/// example, <see cref="Json"/> output is parsed and schema-checked before any
/// content checks; <see cref="Diff"/> output is checked for well-formed,
/// in-scope block edits before being shown to the user.
/// </remarks>
public enum OutputFormat
{
    /// <summary>Free-form plain text (e.g. a chat reply, an autocomplete span).</summary>
    PlainText = 0,

    /// <summary>Markdown-formatted text.</summary>
    Markdown = 1,

    /// <summary>A JSON document expected to match a named schema.</summary>
    Json = 2,

    /// <summary>A structured, block-scoped edit diff awaiting user review.</summary>
    Diff = 3,
}
