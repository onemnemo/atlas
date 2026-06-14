namespace Atlas.Core.Diagnostics;

/// <summary>
/// How serious a <see cref="AtlasWarning"/> is for the consumer of a result.
/// </summary>
public enum WarningSeverity
{
    /// <summary>Informational; the result is fully trustworthy.</summary>
    Info = 0,

    /// <summary>
    /// The result is usable but something was degraded, trimmed, or could not be
    /// verified — the consumer should surface it.
    /// </summary>
    Caution = 1,

    /// <summary>
    /// A part of the task failed. The result, if any, is partial and must be
    /// presented as such.
    /// </summary>
    Error = 2,
}

/// <summary>
/// A structured, user-surfaceable note attached to a result explaining a
/// degradation, uncertainty, or blocked action (arch §26).
/// </summary>
/// <remarks>
/// Warnings are the mechanism by which Atlas refuses to fail silently. They are
/// structured (not free-text log lines) so the UI can render them, telemetry can
/// aggregate them, and tests can assert on them. Every degraded or escalated
/// result must carry at least one warning explaining why.
/// </remarks>
/// <param name="Severity">How serious the warning is.</param>
/// <param name="Mode">The anticipated failure mode this warning corresponds to.</param>
/// <param name="Message">A plain-language explanation safe to show the user.</param>
/// <param name="Detail">
/// Optional developer-facing detail (e.g. which validator failed). Not intended
/// for end users.
/// </param>
public sealed record AtlasWarning(
    WarningSeverity Severity,
    FailureMode Mode,
    string Message,
    string? Detail = null)
{
    /// <summary>Creates an informational warning with no specific failure mode.</summary>
    public static AtlasWarning Info(string message, string? detail = null) =>
        new(WarningSeverity.Info, FailureMode.None, message, detail);

    /// <summary>Creates a caution-level warning for a degraded-but-usable result.</summary>
    public static AtlasWarning Caution(FailureMode mode, string message, string? detail = null) =>
        new(WarningSeverity.Caution, mode, message, detail);

    /// <summary>Creates an error-level warning for a failed or partial result.</summary>
    public static AtlasWarning Error(FailureMode mode, string message, string? detail = null) =>
        new(WarningSeverity.Error, mode, message, detail);
}
