namespace Atlas.Core.Inference;

/// <summary>
/// A single tool invocation the model chose to make, parsed from a
/// <c>finish_reason: "tool_calls"</c> response (arch §12).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ArgumentsJson"/> string is the model's raw JSON for the tool
/// arguments. It must be parsed and validated by the caller before execution —
/// the inference layer just passes it through verbatim, consistent with the
/// principle that the model is untrusted (arch §3, §26).
/// </para>
/// <para>
/// <see cref="CallId"/> is the correlation token the backend assigned so the
/// matching tool-result message can be linked back to this call. It must be
/// preserved and passed back when the result is injected into the conversation.
/// </para>
/// </remarks>
/// <param name="CallId">Backend-assigned ID for correlating this call with its result.</param>
/// <param name="ToolName">The <see cref="ToolDefinition.Name"/> the model requested.</param>
/// <param name="ArgumentsJson">The raw JSON string the model produced for the tool arguments.</param>
public sealed record ToolCallRequest(
    string CallId,
    string ToolName,
    string ArgumentsJson);
