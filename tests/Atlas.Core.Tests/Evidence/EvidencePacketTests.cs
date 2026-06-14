using Atlas.Core.Evidence;
using Xunit;

namespace Atlas.Core.Tests.Evidence;

public sealed class EvidencePacketTests
{
    private static SourceReference SampleReference =>
        new(SourceReferenceKind.NoteBlock, "note-42/block-7", "Photosynthesis intro");

    [Fact]
    public void Finding_requires_at_least_one_source_reference()
    {
        // The core arch §18 invariant: a finding without provenance is not a finding.
        Assert.Throws<ArgumentException>(() =>
            Finding.Create("Plants convert light to energy.", [], ExtractionMethod.ExactMatch, ConfidenceTier.High));
    }

    [Fact]
    public void Finding_requires_a_non_empty_statement()
    {
        Assert.Throws<ArgumentException>(() =>
            Finding.Create("   ", [SampleReference], ExtractionMethod.ExactMatch, ConfidenceTier.High));
    }

    [Fact]
    public void Valid_finding_is_constructed()
    {
        Finding finding = Finding.Create(
            "Plants convert light to chemical energy.",
            [SampleReference],
            ExtractionMethod.Semantic,
            ConfidenceTier.Medium);

        Assert.Single(finding.SourceReferences);
        Assert.Equal(ExtractionMethod.Semantic, finding.ExtractionMethod);
    }

    [Fact]
    public void Empty_packet_reports_no_findings_but_records_why()
    {
        EvidencePacket packet = EvidencePacket.Empty("find notes on mitosis", "no matching notes indexed");

        Assert.False(packet.HasFindings);
        Assert.Contains("no matching notes indexed", packet.MissingInformation);
    }

    [Fact]
    public void Packet_flags_concerns_when_a_finding_is_low_confidence()
    {
        Finding lowConfidence = Finding.Create(
            "Possibly related to topic X.",
            [SampleReference],
            ExtractionMethod.Semantic,
            ConfidenceTier.Low);

        var packet = new EvidencePacket(
            Task: "investigate topic X",
            Findings: [lowConfidence],
            Uncertainty: [],
            Conflicts: [],
            MissingInformation: [],
            SuggestedNextActions: [],
            Warnings: []);

        Assert.True(packet.HasConcerns);
    }

    [Fact]
    public void Source_reference_requires_a_locator()
    {
        var reference = new SourceReference(SourceReferenceKind.Url, "  ");
        Assert.Throws<ArgumentException>(reference.Validate);
    }
}
