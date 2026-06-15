using Atlas.Core.Diagnostics;

namespace Atlas.Core.Tools;

/// <summary>
/// The outcome class of a tool invocation.
/// </summary>
public enum ToolResultStatus
{
    /// <summary>The tool ran and produced a usable result.</summary>
    Ok = 0,

    /// <summary>
    /// The invocation was refused before running — unknown tool, failed
    /// validation, or insufficient permission. The model can correct and retry.
    /// </summary>
    Rejected = 1,

    /// <summary>The tool ran but failed, or its backend was unavailable.</summary>
    Failed = 2,
}

/// <summary>
/// The structured result of invoking a tool (arch §11, §26).
/// </summary>
/// <remarks>
/// <para>
/// Like every Atlas boundary, a tool never fails silently. A rejected or failed
/// invocation carries a <see cref="FailureMode"/> and a plain-language
/// <see cref="Message"/> the orchestrator can surface or feed back to the model
/// for a corrected retry (arch §21, §26).
/// </para>
/// <para>
/// <see cref="Content"/> is the compact, structured finding the main model should
/// receive — not a raw dump (arch §11). For most tools it is JSON or a short text
/// excerpt.
/// </para>
/// </remarks>
/// <param name="Status">The outcome class.</param>
/// <param name="Content">The result payload (often JSON or a short excerpt).</param>
/// <param name="Mode">The failure mode when not <see cref="ToolResultStatus.Ok"/>.</param>
/// <param name="Message">A plain-language explanation, primarily for non-ok results.</param>
public sealed record ToolResult(
    ToolResultStatus Status,
    string Content = "",
    FailureMode Mode = FailureMode.None,
    string? Message = null)
{
    /// <summary>Whether the invocation succeeded.</summary>
    public bool IsOk => Status == ToolResultStatus.Ok;

    /// <summary>Creates a successful result with the given payload.</summary>
    public static ToolResult Ok(string content) =>
        new(ToolResultStatus.Ok, content);

    /// <summary>Creates a rejected result (refused before execution).</summary>
    public static ToolResult Rejected(FailureMode mode, string message) =>
        new(ToolResultStatus.Rejected, string.Empty, mode, message);

    /// <summary>Creates a failed result (ran or attempted, but failed).</summary>
    public static ToolResult Failed(FailureMode mode, string message, string content = "") =>
        new(ToolResultStatus.Failed, content, mode, message);
}
