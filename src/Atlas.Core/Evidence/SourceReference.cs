namespace Atlas.Core.Evidence;

/// <summary>
/// The kind of object a <see cref="SourceReference"/> points at.
/// </summary>
/// <remarks>
/// "Prefer block IDs over prose references" (arch §29). Knowing the kind lets
/// the citation validator pick the right existence check (does this block id
/// resolve? does this URL's domain require the external gate?) without parsing
/// the locator string.
/// </remarks>
public enum SourceReferenceKind
{
    /// <summary>A whole note.</summary>
    Note = 0,

    /// <summary>A specific block within a note (the preferred granularity).</summary>
    NoteBlock = 1,

    /// <summary>A chunk of an uploaded/indexed file or PDF.</summary>
    FileChunk = 2,

    /// <summary>A node within a mindmap.</summary>
    MindmapNode = 3,

    /// <summary>A unit or section within a learning path.</summary>
    LearningPathUnit = 4,

    /// <summary>A flashcard.</summary>
    Flashcard = 5,

    /// <summary>An external URL (only valid when the external gate is open).</summary>
    Url = 6,
}

/// <summary>
/// A resolvable pointer back to the origin of a finding (arch §18).
/// </summary>
/// <remarks>
/// Every finding must carry at least one of these. "A finding without a source
/// reference and extraction method is not a finding — it is an unverifiable
/// claim" (arch §18). The <see cref="Locator"/> is the stable identifier
/// (block id, chunk id, node id, or URL) that a deterministic validator can
/// check for existence.
/// </remarks>
/// <param name="Kind">What kind of object is referenced.</param>
/// <param name="Locator">
/// The stable identifier of the referenced object — a block id, chunk id, node
/// id, unit id, or URL. Must be non-empty.
/// </param>
/// <param name="Label">
/// An optional human-readable label for display (e.g. a note title or heading).
/// Never used for resolution; resolution always uses <see cref="Locator"/>.
/// </param>
public sealed record SourceReference(
    SourceReferenceKind Kind,
    string Locator,
    string? Label = null)
{
    /// <summary>Validates that the reference carries a usable locator.</summary>
    /// <exception cref="ArgumentException"><see cref="Locator"/> is empty.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Locator))
        {
            throw new ArgumentException("SourceReference.Locator must be non-empty.", nameof(Locator));
        }
    }
}
