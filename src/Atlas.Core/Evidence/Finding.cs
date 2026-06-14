using System.Collections.Immutable;

namespace Atlas.Core.Evidence;

/// <summary>
/// A single structured result inside an <see cref="EvidencePacket"/> (arch §18).
/// </summary>
/// <remarks>
/// A finding is deliberately a structured claim plus its provenance, not a prose
/// sentence. The provenance (<see cref="SourceReferences"/>,
/// <see cref="ExtractionMethod"/>, <see cref="Confidence"/>) is what lets the
/// main agent and validators decide how much to trust it. A finding with no
/// source references is not constructible through <see cref="Create"/>.
/// </remarks>
/// <param name="Statement">The claim or extracted value, stated plainly.</param>
/// <param name="SourceReferences">
/// One or more resolvable pointers back to where the claim came from.
/// </param>
/// <param name="ExtractionMethod">How the claim was located.</param>
/// <param name="Confidence">How strongly the source supports the claim.</param>
public sealed record Finding(
    string Statement,
    ImmutableArray<SourceReference> SourceReferences,
    ExtractionMethod ExtractionMethod,
    ConfidenceTier Confidence)
{
    /// <summary>
    /// Creates a validated finding, guaranteeing the traceability invariant from
    /// arch §18 (a finding must have a statement and at least one resolvable
    /// source reference).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The statement is empty or no source references were supplied.
    /// </exception>
    public static Finding Create(
        string statement,
        IEnumerable<SourceReference> sourceReferences,
        ExtractionMethod extractionMethod,
        ConfidenceTier confidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        ArgumentNullException.ThrowIfNull(sourceReferences);

        ImmutableArray<SourceReference> refs = [.. sourceReferences];
        if (refs.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "A Finding must cite at least one source reference (arch §18).",
                nameof(sourceReferences));
        }

        foreach (SourceReference reference in refs)
        {
            reference.Validate();
        }

        return new Finding(statement, refs, extractionMethod, confidence);
    }
}
