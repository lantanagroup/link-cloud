using LantanaGroup.Link.Normalization.Application.Models;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace UnitTests.Normalization;

/// <summary>
/// Covers the roll-up from per-resource code map counts to the per-patient outcome placed on
/// MappingOutcomeEvaluated, and the status each combination of counts projects to.
/// </summary>
[Trait("Category", "UnitTests")]
public class MappingOutcomeAccumulatorTests
{
    private const string LocalSystem = "http://hospital.example.org/locations";
    private const string OtherLocalSystem = "urn:oid:1.2.840.114350.1.13.277.3.7.2.686990";
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    [Fact]
    public void NothingAdded_BuildsNoOutcomes()
    {
        var accumulator = new MappingOutcomeAccumulator();

        // A facility that configured no code maps reports an empty list, which is itself the answer --
        // the consumer reads it as "not applicable" rather than "nothing was said".
        Assert.Empty(accumulator.BuildAll());
    }

    [Fact]
    public void NullOutcomes_MoveNoTally()
    {
        var accumulator = new MappingOutcomeAccumulator();

        // Every non-code-map operation returns null here, as does a code map that matched nothing at its
        // FHIRPath. Neither is a failure and neither is an unmapped code.
        accumulator.Add(null);

        Assert.Empty(accumulator.BuildAll());
    }

    [Fact]
    public void CountsAccumulateAcrossResources()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 2, 1, ["PHARMACY"])]);
        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 3, 2, ["LAB", "IMAGING"])]);

        // One correlation is one patient, so the totals span every resource that patient contributed.
        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(5, outcome.MappedCount);
        Assert.Equal(3, outcome.UnmappedCount);
        Assert.Equal(["IMAGING", "LAB", "PHARMACY"], outcome.UnmappedCodes.OrderBy(code => code));
    }

    [Fact]
    public void SameCodeFromDifferentResources_IsListedOnce()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 0, 1, ["PHARMACY"])]);
        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 0, 1, ["PHARMACY"])]);

        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(2, outcome.UnmappedCount);
        Assert.Equal("PHARMACY", Assert.Single(outcome.UnmappedCodes));
    }

    [Fact]
    public void TwoSourceSystemsIntoOneTarget_StayDistinct()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 4, 0, [])]);
        accumulator.Add([new CodeMappingOutcome(OtherLocalSystem, HslocSystem, 0, 2, ["BED", "WARD"])]);

        // Merging on target alone would report the pair as partially mapped and lose which source failed.
        var outcomes = accumulator.BuildAll();
        Assert.Equal(2, outcomes.Count);
        Assert.Equal(MappingStatus.Mapped, Assert.Single(outcomes, o => o.SourceSystem == LocalSystem).Status);
        Assert.Equal(MappingStatus.Unmapped, Assert.Single(outcomes, o => o.SourceSystem == OtherLocalSystem).Status);
    }

    [Theory]
    [InlineData(5, 0, MappingStatus.Mapped)]
    [InlineData(0, 5, MappingStatus.Unmapped)]
    [InlineData(3, 2, MappingStatus.PartiallyMapped)]
    public void StatusIsProjectedFromTheCounts(int mapped, int unmapped, MappingStatus expected)
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, mapped, unmapped, [])]);

        Assert.Equal(expected, Assert.Single(accumulator.BuildAll()).Status);
    }

    [Fact]
    public void FailuresOnly_ReportUnknownRatherThanUnmapped()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.AddFailure(CodeMapOperation(Map(LocalSystem, HslocSystem)));

        // Unknown, not Unmapped: a processing fault must not be reported as a gap in the facility's
        // configuration, and must not be hidden as a success either.
        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(MappingStatus.Unknown, outcome.Status);
        Assert.Equal(1, outcome.FailureCount);
        Assert.Equal(0, outcome.MappedCount);
        Assert.Equal(0, outcome.UnmappedCount);
    }

    [Fact]
    public void FailureAlongsideRealCounts_KeepsTheCountsAuthoritative()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 3, 1, ["PHARMACY"])]);
        accumulator.AddFailure(CodeMapOperation(Map(LocalSystem, HslocSystem)));

        // One resource failing does not erase what the others established; the status still describes the
        // mapping, with the failure recorded alongside it.
        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(MappingStatus.PartiallyMapped, outcome.Status);
        Assert.Equal(1, outcome.FailureCount);
    }

    [Fact]
    public void FailureRecordsEveryConfiguredPair_NotOnlyTheOnesExercised()
    {
        var accumulator = new MappingOutcomeAccumulator();

        // A thrown operation yields no per-coding outcomes, so the pairs come from what it was configured
        // with rather than from results.
        accumulator.AddFailure(CodeMapOperation(
            Map(LocalSystem, HslocSystem),
            Map(OtherLocalSystem, HslocSystem)));

        var outcomes = accumulator.BuildAll();
        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.Equal(MappingStatus.Unknown, outcome.Status));
    }

    [Fact]
    public void UnmappedCodeSampleIsCapped_WhileTheCountStaysTrue()
    {
        var accumulator = new MappingOutcomeAccumulator();

        var codes = Enumerable.Range(0, 50).Select(index => $"CODE-{index}").ToList();
        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 0, codes.Count, codes)]);

        // The list is a troubleshooting sample; a facility whose code map is empty would otherwise put
        // every code it saw on the wire. UnmappedCount remains the authoritative total.
        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(50, outcome.UnmappedCount);
        Assert.Equal(20, outcome.UnmappedCodes.Count);
    }

    [Fact]
    public void CapAppliesAcrossResources_NotPerResource()
    {
        var accumulator = new MappingOutcomeAccumulator();

        foreach (var batch in Enumerable.Range(0, 5))
        {
            var codes = Enumerable.Range(0, 10).Select(index => $"BATCH-{batch}-CODE-{index}").ToList();
            accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 0, codes.Count, codes)]);
        }

        // The budget belongs to the patient, not to each resource, or a patient with many resources would
        // put an unbounded list on the wire.
        var outcome = Assert.Single(accumulator.BuildAll());
        Assert.Equal(50, outcome.UnmappedCount);
        Assert.Equal(20, outcome.UnmappedCodes.Count);
    }

    [Fact]
    public void TargetSystemsAreNotNormalized()
    {
        var accumulator = new MappingOutcomeAccumulator();

        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem, 1, 0, [])]);
        accumulator.Add([new CodeMappingOutcome(LocalSystem, HslocSystem.Replace("https://", "http://"), 1, 0, [])]);

        // An admin who typed the wrong scheme produced resources genuinely carrying the wrong system, which
        // downstream evaluation will not match. Folding the two together would report that as success.
        Assert.Equal(2, accumulator.BuildAll().Count);
    }

    private static CodeSystemMap Map(string sourceSystem, string targetSystem) =>
        new(sourceSystem, targetSystem, new Dictionary<string, CodeMap>());

    private static CodeMapOperation CodeMapOperation(params CodeSystemMap[] maps) =>
        new("Location type to HSLOC", "Location.type.coding", maps.ToList());
}
