namespace Atlas.Core.Results;

/// <summary>
/// The overall disposition of a pipeline run or stage (arch §26).
/// </summary>
/// <remarks>
/// The distinction between <see cref="Degraded"/>, <see cref="Escalated"/>, and
/// <see cref="Failed"/> is central to the architecture's promise never to fail
/// silently. A degraded result still carries usable output (with warnings); an
/// escalated result is handing the decision back to the user; a failed result
/// produced nothing usable and says so plainly.
/// </remarks>
public enum OutcomeStatus
{
    /// <summary>Completed cleanly; output is fully trustworthy.</summary>
    Success = 0,

    /// <summary>
    /// Completed with usable output, but something was trimmed, unverifiable, or
    /// partially failed. Always accompanied by warnings explaining what.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Could not complete autonomously and is deferring to the user (e.g. a
    /// destructive action that failed validation, or an exhausted repair loop).
    /// </summary>
    Escalated = 2,

    /// <summary>Produced no usable output. Accompanied by error-level warnings.</summary>
    Failed = 3,
}
