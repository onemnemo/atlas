using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Atlas.Core.Tools;

/// <summary>
/// The JSON value kind a <see cref="ToolParameter"/> accepts.
/// </summary>
/// <remarks>
/// Deliberately a small, closed set. Tool arguments for small models must be
/// simple and schema-constrained (arch §14, §35); deeply nested argument shapes
/// invite malformed output. Complex inputs should be expressed as an opaque
/// <see cref="Object"/> whose meaning the tool documents in prose.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Members deliberately mirror JSON Schema primitive type names so the schema mapping is one-to-one and obvious.")]
public enum ToolParameterType
{
    /// <summary>A UTF-8 string.</summary>
    String = 0,

    /// <summary>A whole number.</summary>
    Integer = 1,

    /// <summary>A real number.</summary>
    Number = 2,

    /// <summary>A boolean flag.</summary>
    Boolean = 3,

    /// <summary>A nested JSON object.</summary>
    Object = 4,

    /// <summary>A JSON array.</summary>
    Array = 5,
}

/// <summary>
/// One declared argument of a tool, with enough structure to emit a JSON Schema
/// and to validate a model's tool-call arguments before execution (arch §14, §35).
/// </summary>
/// <param name="Name">The argument name, used as the JSON property key.</param>
/// <param name="Type">The accepted JSON value kind.</param>
/// <param name="Description">A short, model-facing description of the argument.</param>
/// <param name="Required">Whether the argument must be present.</param>
/// <param name="AllowedValues">
/// Optional closed set of permitted string values (an enum constraint). Empty
/// means unconstrained.
/// </param>
public sealed record ToolParameter(
    string Name,
    ToolParameterType Type,
    string Description,
    bool Required = true,
    ImmutableArray<string> AllowedValues = default)
{
    /// <summary>The permitted values, normalised to a non-default empty array.</summary>
    public ImmutableArray<string> AllowedValues { get; init; } =
        AllowedValues.IsDefault ? ImmutableArray<string>.Empty : AllowedValues;

    /// <summary>Creates a string parameter.</summary>
    public static ToolParameter Text(string name, string description, bool required = true) =>
        new(name, ToolParameterType.String, description, required);

    /// <summary>Creates a whole-number parameter.</summary>
    public static ToolParameter Whole(string name, string description, bool required = true) =>
        new(name, ToolParameterType.Integer, description, required);
}
