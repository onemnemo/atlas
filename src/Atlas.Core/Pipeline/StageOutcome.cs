using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Atlas.Core.Diagnostics;
using Atlas.Core.Results;

namespace Atlas.Core.Pipeline;

/// <summary>
/// The result of executing one pipeline stage: a typed value plus its honest
/// disposition and any warnings (arch §26).
/// </summary>
/// <remarks>
/// <para>
/// Stages return a <see cref="StageOutcome{T}"/> rather than throwing for
/// <em>expected</em> failures (malformed model output, empty retrieval, failed
/// validation). Exceptions are reserved for genuine programming errors. This
/// keeps degradation a first-class, inspectable value that flows through the
/// pipeline instead of an exception that unwinds it.
/// </para>
/// <para>
/// The <see cref="Value"/> is present for <see cref="OutcomeStatus.Success"/> and
/// <see cref="OutcomeStatus.Degraded"/>, may be present for
/// <see cref="OutcomeStatus.Escalated"/> (a partial result handed to the user),
/// and is absent for <see cref="OutcomeStatus.Failed"/>.
/// </para>
/// <para>
/// Construct outcomes through the factory methods on the non-generic
/// <see cref="StageOutcome"/> companion class, which give clean type inference
/// (e.g. <c>StageOutcome.Success(value)</c>).
/// </para>
/// </remarks>
/// <typeparam name="T">The stage's typed output, defined by its output contract.</typeparam>
/// <param name="Status">The honest disposition of the stage.</param>
/// <param name="Value">The produced value, or <see langword="null"/> when none was produced.</param>
/// <param name="Warnings">Warnings accumulated by the stage.</param>
public sealed record StageOutcome<T>(
    OutcomeStatus Status,
    T? Value,
    ImmutableArray<AtlasWarning> Warnings)
{
    /// <summary>Whether a usable value is present.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => Value is not null;
}

/// <summary>
/// Factory methods for <see cref="StageOutcome{T}"/>.
/// </summary>
/// <remarks>
/// Kept as a non-generic companion (the <c>Task</c>/<c>Task&lt;T&gt;</c> pattern)
/// so callers get type inference and the factories are not static members on a
/// generic type.
/// </remarks>
public static class StageOutcome
{
    /// <summary>A clean success carrying <paramref name="value"/>.</summary>
    public static StageOutcome<T> Success<T>(T value) =>
        new(OutcomeStatus.Success, value, []);

    /// <summary>
    /// A degraded-but-usable outcome carrying <paramref name="value"/> and the
    /// warnings explaining the degradation (at least one required).
    /// </summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static StageOutcome<T> Degraded<T>(T value, params AtlasWarning[] warnings)
    {
        if (warnings is null || warnings.Length == 0)
        {
            throw new ArgumentException("A degraded outcome must carry at least one warning.", nameof(warnings));
        }

        return new StageOutcome<T>(OutcomeStatus.Degraded, value, [.. warnings]);
    }

    /// <summary>
    /// An escalation: the stage is deferring to the user, optionally with a partial
    /// <paramref name="partialValue"/> (at least one warning required).
    /// </summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static StageOutcome<T> Escalated<T>(IEnumerable<AtlasWarning> warnings, T? partialValue = default)
    {
        ImmutableArray<AtlasWarning> w = [.. warnings];
        if (w.IsDefaultOrEmpty)
        {
            throw new ArgumentException("An escalation must explain itself with at least one warning.", nameof(warnings));
        }

        return new StageOutcome<T>(OutcomeStatus.Escalated, partialValue, w);
    }

    /// <summary>A hard failure with no value, explained by error-level warnings (at least one required).</summary>
    /// <exception cref="ArgumentException">No warnings were supplied.</exception>
    public static StageOutcome<T> Failed<T>(params AtlasWarning[] warnings)
    {
        if (warnings is null || warnings.Length == 0)
        {
            throw new ArgumentException("A failed outcome must explain itself with at least one warning.", nameof(warnings));
        }

        return new StageOutcome<T>(OutcomeStatus.Failed, default, [.. warnings]);
    }
}
