namespace Atlas.Core.Inference;

/// <summary>
/// The outcome of one round of model-directed tool calls: the calls the model
/// made and the result each produced (arch §12).
/// </summary>
/// <remarks>
/// <para>
/// A round captures one complete step of the tool-call loop:
/// </para>
/// <list type="number">
/// <item><description>
/// Model emits <see cref="Calls"/> (one entry per tool it chose to invoke).
/// </description></item>
/// <item><description>
/// The orchestration layer executes each call and collects <see cref="Results"/>
/// (parallel to <see cref="Calls"/> by index).
/// </description></item>
/// <item><description>
/// Both are injected back into the conversation history as an assistant
/// <c>tool_calls</c> message followed by per-call <c>role: tool</c> messages.
/// </description></item>
/// </list>
/// <para>
/// The list of rounds is passed back to <see cref="IInferenceClient"/> on
/// each subsequent call so the model has full visibility of what happened.
/// </para>
/// </remarks>
/// <param name="Calls">The tool invocations the model emitted this round.</param>
/// <param name="Results">
/// The result content for each call, indexed parallel to <paramref name="Calls"/>.
/// </param>
public sealed record ToolRoundResult(
    IReadOnlyList<ToolCallRequest> Calls,
    IReadOnlyList<string> Results);
