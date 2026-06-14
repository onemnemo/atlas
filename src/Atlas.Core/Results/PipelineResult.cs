using System.Collections.Immutable;
using Atlas.Core.Diagnostics;

namespace Atlas.Core.Results;

/// <summary>
/// The public output of an orchestrator run (arch §31.1).
/// </summary>
/// <remarks>
/// <para>
/// A result always reports its <see cref="Status"/> honestly and carries any
/// <see cref="Warnings"/> that explain a degradation, escalation, or failure.
/// The architecture's core promise is encoded here: the system never pretends to
/// have succeeded (arch §26). A <see cref="Content"/> of <see langword="null"/>
/// is a legitimate outcome for a hard failure and must be paired with at least
/// one error-level warning.
/// </para>
/// <para>
/// <see cref="Content"/> is a string because the public boundary stays simple
/// and serializable: structured outputs are serialized according to their
/// <see cref="Contracts.OutputContract"/> (typically JSON) and named by
/// <see cref="OutputType"/>, so an in-process or IPC consumer handles both the
/// same way.
/// </para>
/// </remarks>
/// <param name="Status">The honest disposition of the run.</param>
/// <param name="Content">
/// The produced output, serialized per its output contract; <see langword="null"/>
/// only on hard failure.
/// </param>
/// <param name="OutputType">
/// The <see cref="Contracts.OutputContract.OutputType"/> describing how to
/// interpret <see cref="Content"/>, or <see langword="null"/> if there is none.
/// </param>
/// <param name="Warnings">All warnings accumulated during the run.</param>
/// <param name="RequestId">The originating request's id, for correlation.</param>
public sealed record PipelineResult(
    OutcomeStatus Status,
    string? Content,
    string? OutputType,
    ImmutableArray<AtlasWarning> Warnings,
    Guid RequestId)
{
    /// <summary>Whether the run produced output the caller can use (success or degraded).</summary>
    public bool HasUsableOutput => Status is OutcomeStatus.Success or OutcomeStatus.Degraded && Content is not null;

    /// <summary>Creates a clean success result.</summary>
    public static PipelineResult Success(Guid requestId, string content, string outputType) =>
        new(OutcomeStatus.Success, content, outputType, [], requestId);

    /// <summary>
    /// Creates a degraded result: usable output plus the warnings explaining the
    /// degradation. At least one warning is required.
    /// </summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static PipelineResult Degraded(
        Guid requestId,
        string content,
        string outputType,
        IEnumerable<AtlasWarning> warnings)
    {
        ImmutableArray<AtlasWarning> w = [.. warnings];
        if (w.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A degraded result must carry at least one warning.", nameof(warnings));
        }

        return new PipelineResult(OutcomeStatus.Degraded, content, outputType, w, requestId);
    }

    /// <summary>
    /// Creates an escalated result: Atlas is deferring the decision to the user,
    /// optionally with partial content. Carries the warnings explaining why the
    /// run could not complete autonomously (at least one required).
    /// </summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static PipelineResult Escalated(
        Guid requestId,
        IEnumerable<AtlasWarning> warnings,
        string? partialContent = null,
        string? outputType = null)
    {
        ImmutableArray<AtlasWarning> w = [.. warnings];
        if (w.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An escalated result must explain itself with at least one warning.", nameof(warnings));
        }

        return new PipelineResult(OutcomeStatus.Escalated, partialContent, outputType, w, requestId);
    }

    /// <summary>
    /// Creates a failed result with no usable output and the error-level warnings
    /// explaining why. At least one warning is required.
    /// </summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static PipelineResult Failed(Guid requestId, IEnumerable<AtlasWarning> warnings)
    {
        ImmutableArray<AtlasWarning> w = [.. warnings];
        if (w.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A failed result must explain itself with at least one warning.", nameof(warnings));
        }

        return new PipelineResult(OutcomeStatus.Failed, Content: null, OutputType: null, w, requestId);
    }
}
