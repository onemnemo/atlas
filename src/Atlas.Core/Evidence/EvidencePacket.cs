using System.Collections.Immutable;

namespace Atlas.Core.Evidence;

/// <summary>
/// The structured result a subagent returns to the main agent (arch §18).
/// </summary>
/// <remarks>
/// <para>
/// Subagents "must not return plain summaries only" (arch §18). Plain summaries
/// silently drop citations, hide uncertainty, and break references. The evidence
/// packet forces the alternative: structured findings with provenance, plus
/// explicit honesty about what is uncertain, contradictory, or missing.
/// </para>
/// <para>
/// The negative-space fields (<see cref="Uncertainty"/>, <see cref="Conflicts"/>,
/// <see cref="MissingInformation"/>, <see cref="Warnings"/>) are first-class on
/// purpose. With small, unreliable models the things a subagent <em>could not</em>
/// establish are as important to the main agent as the things it could. An empty
/// packet that is honest about finding nothing is a valid, useful result.
/// </para>
/// </remarks>
/// <param name="Task">The specific subtask this packet answers.</param>
/// <param name="Findings">The structured results, each with its own provenance.</param>
/// <param name="Uncertainty">What the subagent could not determine.</param>
/// <param name="Conflicts">Contradictory findings observed across sources.</param>
/// <param name="MissingInformation">What was expected but not found.</param>
/// <param name="SuggestedNextActions">What the main agent might do with these findings.</param>
/// <param name="Warnings">Anything that should be flagged before the findings are used.</param>
public sealed record EvidencePacket(
    string Task,
    ImmutableArray<Finding> Findings,
    ImmutableArray<string> Uncertainty,
    ImmutableArray<string> Conflicts,
    ImmutableArray<string> MissingInformation,
    ImmutableArray<string> SuggestedNextActions,
    ImmutableArray<string> Warnings)
{
    /// <summary>
    /// Creates a packet reporting that a subtask completed but found nothing.
    /// </summary>
    /// <remarks>
    /// This is the honest degradation for "retrieval returns nothing" (arch §26):
    /// an explicit empty result with a note in <see cref="MissingInformation"/>,
    /// never a fabricated finding.
    /// </remarks>
    /// <param name="task">The subtask that was attempted.</param>
    /// <param name="note">Why nothing was found, recorded as missing information.</param>
    public static EvidencePacket Empty(string task, string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(note);

        return new EvidencePacket(
            Task: task,
            Findings: [],
            Uncertainty: [],
            Conflicts: [],
            MissingInformation: [note],
            SuggestedNextActions: [],
            Warnings: []);
    }

    /// <summary>Whether the packet carries any findings at all.</summary>
    public bool HasFindings => !Findings.IsDefaultOrEmpty;

    /// <summary>
    /// Whether the packet flags any concern the main agent should weigh before
    /// trusting it — conflicts, warnings, or low-confidence findings.
    /// </summary>
    public bool HasConcerns =>
        !Conflicts.IsDefaultOrEmpty
        || !Warnings.IsDefaultOrEmpty
        || (!Findings.IsDefaultOrEmpty && Findings.Any(static f => f.Confidence == ConfidenceTier.Low));
}
