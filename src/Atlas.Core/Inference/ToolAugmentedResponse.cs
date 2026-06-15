namespace Atlas.Core.Inference;

/// <summary>
/// The result of a tool-augmented completion call: either the model wants to
/// call tools, or it produced a final text response (arch §12).
/// </summary>
/// <remarks>
/// <para>
/// This discriminated union separates the two distinct outcomes of a
/// <see cref="IInferenceClient.CompleteWithToolsAsync"/> call:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="IsToolCall"/> true — the model emitted
/// <c>finish_reason: "tool_calls"</c>; check <see cref="ToolCalls"/> for what
/// to execute.
/// </description></item>
/// <item><description>
/// <see cref="IsToolCall"/> false — the model produced a normal text reply;
/// read <see cref="TextResponse"/>.
/// </description></item>
/// </list>
/// <para>
/// The orchestration layer drives the tool-call loop: execute each call, append
/// results to the conversation via another <see cref="IInferenceClient.CompleteWithToolsAsync"/>
/// call, and repeat until <see cref="IsToolCall"/> is false or the iteration
/// cap is reached (arch §21 — bounded repair loop).
/// </para>
/// </remarks>
public sealed class ToolAugmentedResponse
{
    private ToolAugmentedResponse() { }

    /// <summary>True when the model chose to call tools rather than producing a final reply.</summary>
    public bool IsToolCall { get; private init; }

    /// <summary>The tool calls emitted by the model. Non-null when <see cref="IsToolCall"/> is true.</summary>
    public IReadOnlyList<ToolCallRequest>? ToolCalls { get; private init; }

    /// <summary>The final inference response. Non-null when <see cref="IsToolCall"/> is false.</summary>
    public InferenceResponse? TextResponse { get; private init; }

    /// <summary>Creates a tool-calling response.</summary>
    public static ToolAugmentedResponse ToolCalling(IReadOnlyList<ToolCallRequest> calls) =>
        new() { IsToolCall = true, ToolCalls = calls };

    /// <summary>Creates a final text response.</summary>
    public static ToolAugmentedResponse Text(InferenceResponse response) =>
        new() { IsToolCall = false, TextResponse = response };
}
