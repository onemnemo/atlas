namespace Atlas.Core.Inference;

/// <summary>
/// A tool that can be offered to a model during a chat completion request
/// (arch §12 — scoped tool selection).
/// </summary>
/// <remarks>
/// This is the minimal, backend-agnostic representation of a tool's contract.
/// It carries what the model needs to decide whether and how to invoke the tool.
/// Conversion from the richer <c>ToolDescriptor</c> (which carries permissions,
/// gating, and metadata) is done outside this type so the inference layer stays
/// free of tool-policy knowledge.
/// </remarks>
/// <param name="Name">The tool's canonical name (e.g. <c>web.search</c>).</param>
/// <param name="Description">A one-sentence description the model reads to decide whether to use the tool.</param>
/// <param name="ParametersSchemaJson">
/// A JSON Schema <c>object</c> string describing the tool's arguments. Must be
/// valid JSON; if empty or null, the model sees a parameter-less tool.
/// </param>
public sealed record ToolDefinition(
    string Name,
    string Description,
    string? ParametersSchemaJson = null);
