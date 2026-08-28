using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace UnitTests.Report;

/// <summary>
/// Covers how the code map outcomes Normalization reports resolve to the stored HSLOC indicator, and how
/// the outcomes of a patient's separate acquisition passes combine.
/// </summary>
[Trait("Category", "UnitTests")]
public class CodeMapIndicatorTests
{
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    private const string HslocOid = "urn:oid:2.16.840.1.113883.6.259";
    private const string LocalSystem = "http://hospital.example.org/locations";
    private const string OtherLocalSystem = "urn:oid:1.2.840.114350.1.13.277.3.7.2.686990";

    #region HSLOC resolution

    [Fact]
    public void HslocStatusIgnoresTargetSystemsThatAreNotHsloc()
    {
        var status = CodeMapIndicator.ResolveHsloc([
            CodeMap(HslocSystem, mapped: 4, unmapped: 0),
            CodeMap(LocalSystem, mapped: 0, unmapped: 9)
        ]);

        // The unrelated code map is fully unmapped; letting it contribute would report the HSLOC column as
        // broken because of a map that has nothing to do with it.
        Assert.Equal(MappingIndicatorStatus.Mapped, status);
    }

    [Fact]
    public void HslocStatusRecognizesTheOidAsWellAsTheUrl()
    {
        var status = CodeMapIndicator.ResolveHsloc([CodeMap(HslocOid, mapped: 2, unmapped: 0)]);

        Assert.Equal(MappingIndicatorStatus.Mapped, status);
    }

    [Fact]
    public void HslocStatusSumsEverySourceSystemMappingIntoHsloc()
    {
        var status = CodeMapIndicator.ResolveHsloc([
            CodeMap(HslocSystem, mapped: 4, unmapped: 0, sourceSystem: LocalSystem),
            CodeMap(HslocSystem, mapped: 0, unmapped: 3, sourceSystem: OtherLocalSystem)
        ]);

        // A facility may map several local systems into HSLOC. Taking either outcome alone would report
        // the column as fully mapped or fully unmapped; the totals say partially.
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, status);
    }

    [Fact]
    public void NoHslocOutcome_IsNotApplicableRatherThanNotEvaluated()
    {
        var status = CodeMapIndicator.ResolveHsloc([]);

        // The message was authoritative and reported nothing for HSLOC, which is a result. NotEvaluated
        // would claim Normalization had not run for this patient at all.
        Assert.Equal(MappingIndicatorStatus.NotApplicable, status);
    }

    [Fact]
    public void FailuresWithNoCounts_ReportUnknown()
    {
        var status = CodeMapIndicator.ResolveHsloc([
            new CodeMapOutcome(LocalSystem, HslocSystem, MappingStatus.Unknown, 0, 0, 2, [])
        ]);

        // A processing fault is neither a mapping success nor a configuration gap.
        Assert.Equal(MappingIndicatorStatus.Unknown, status);
    }

    #endregion

    #region Combining passes

    [Fact]
    public void TwoPassesReportingTheSamePair_SumIntoOneTotal()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 2, unmapped: 1, unmappedCodes: ["PHARMACY"])),
            "Supplemental",
            CodeMap(HslocSystem, mapped: 3, unmapped: 2, unmappedCodes: ["PHARMACY", "MORGUE"]));

        var outcome = Assert.Single(details.CodeMaps);
        Assert.Equal(5, outcome.MappedCount);
        Assert.Equal(3, outcome.UnmappedCount);

        // The codes are the set an operator would go and configure, so a code seen in both passes is one
        // entry while the counts stay the true totals.
        Assert.Equal(["MORGUE", "PHARMACY"], outcome.UnmappedCodes.OrderBy(code => code));
    }

    [Fact]
    public void AnEmptySupplementalPass_DoesNotEraseThePassBeforeIt()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0)),
            "Supplemental");

        // The defect this exists for. A reportable patient's supplemental pass acquires no Location, so it
        // reports nothing -- and replacing the whole blob would turn a Mapped patient into NotApplicable.
        var outcome = Assert.Single(details.CodeMaps);
        Assert.Equal(1, outcome.MappedCount);
        Assert.Equal(MappingIndicatorStatus.Mapped, CodeMapIndicator.ResolveHsloc(details.CodeMaps));
    }

    [Fact]
    public void RecordingOnePassTwice_LeavesTheTotalsUnchanged()
    {
        var outcome = CodeMap(HslocSystem, mapped: 2, unmapped: 1, unmappedCodes: ["PHARMACY"]);

        var once = Record(null, "Initial", outcome);
        var twice = Record(once, "Initial", outcome);

        // Under at-least-once delivery a message can arrive again after it was already written. Adding
        // would double the patient's counts; the pass identity makes the repeat replace itself.
        Assert.Equal(2, Assert.Single(twice.CodeMaps).MappedCount);
        Assert.Equal(1, Assert.Single(twice.CodeMaps).UnmappedCount);
        Assert.Single(twice.Passes);
    }

    [Fact]
    public void SamePassTypeUnderADifferentCorrelation_IsADifferentPass()
    {
        var first = Record(null, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0), correlationId: "a");
        var second = Record(first, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0), correlationId: "b");

        // A patient can be acquired more than once for one report. Those are separate acquisitions whose
        // results both count, not a repeat of one.
        Assert.Equal(2, second.Passes.Count);
        Assert.Equal(2, Assert.Single(second.CodeMaps).MappedCount);
    }

    [Fact]
    public void MappingsFoundOnlyInTheSupplementalPass_AreAdded()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0)),
            "Supplemental",
            CodeMap(OtherLocalSystem, mapped: 0, unmapped: 4, unmappedCodes: ["BED"]));

        // The supplemental pass acquires resources the initial one did not, so it can be the first to
        // exercise a map. Its outcome is new information, not a correction of the earlier pass.
        Assert.Equal(2, details.CodeMaps.Count);
        Assert.Single(details.CodeMaps, outcome => outcome.TargetSystem == OtherLocalSystem && outcome.UnmappedCount == 4);
    }

    [Fact]
    public void PassesDisagreeingOnOnePair_ResolveFromTheCombinedTotals()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 3, unmapped: 0)),
            "Supplemental",
            CodeMap(HslocSystem, mapped: 0, unmapped: 2, unmappedCodes: ["PHARMACY"]));

        // Either pass alone reads as a clean Mapped or a clean Unmapped. Only the combination is true, and
        // it is the one an operator needs: some of this patient's locations have no HSLOC code.
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, CodeMapIndicator.ResolveHsloc(details.CodeMaps));
        Assert.Equal("PHARMACY", Assert.Single(Assert.Single(details.CodeMaps).UnmappedCodes));
    }

    [Fact]
    public void PairsAreKeptDistinctBySourceSystemAsWellAsTarget()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0, sourceSystem: LocalSystem)),
            "Supplemental",
            CodeMap(HslocSystem, mapped: 0, unmapped: 1, sourceSystem: OtherLocalSystem));

        // Collapsing on target alone would sum two of the facility's systems into one tally and lose which
        // source system is the one missing codes.
        Assert.Equal(2, details.CodeMaps.Count);
        Assert.Single(details.CodeMaps, outcome => outcome.SourceSystem == OtherLocalSystem && outcome.UnmappedCount == 1);
    }

    [Fact]
    public void TotalsCarryTheStatusOfTheirCombinedCounts()
    {
        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 2, unmapped: 0)),
            "Supplemental",
            CodeMap(HslocSystem, mapped: 0, unmapped: 1));

        // The per-outcome Status rides in the stored blob, so a stale one read back later would contradict
        // the counts beside it.
        Assert.Equal(MappingStatus.PartiallyMapped, Assert.Single(details.CodeMaps).Status);
    }

    [Fact]
    public void CombiningTwoCappedCodeLists_StaysWithinTheCap()
    {
        var first = Enumerable.Range(0, 20).Select(index => $"FIRST-{index}").ToList();
        var second = Enumerable.Range(0, 20).Select(index => $"SECOND-{index}").ToList();

        var details = Record(
            Record(null, "Initial", CodeMap(HslocSystem, mapped: 0, unmapped: 20, unmappedCodes: first)),
            "Supplemental",
            CodeMap(HslocSystem, mapped: 0, unmapped: 20, unmappedCodes: second));

        // Each pass caps its own list, so combining two full ones would otherwise double the blob on every
        // pass. The counts are unaffected -- they are totals, not a list length.
        var outcome = Assert.Single(details.CodeMaps);
        Assert.Equal(20, outcome.UnmappedCodes.Count);
        Assert.Equal(40, outcome.UnmappedCount);
    }

    [Fact]
    public void RecordingAgainstNothingStored_KeepsTheIncomingOutcomes()
    {
        var details = Record(null, "Initial", CodeMap(HslocSystem, mapped: 1, unmapped: 0));

        // The first pass for a patient has no stored blob to read back.
        Assert.Equal(1, Assert.Single(details.CodeMaps).MappedCount);
        Assert.Single(details.Passes);
    }

    [Fact]
    public void DetailsStoredBeforePassesWereRecorded_AreKeptAsOneUnnamedPass()
    {
        // A row written by an earlier build carries totals but no pass breakdown. Discarding them would
        // lose a real earlier result; treating them as one unnamed pass keeps the totals honest.
        var legacy = new NormalizationMappingDetails([CodeMap(HslocSystem, mapped: 4, unmapped: 0)], []);

        var details = Record(legacy, "Supplemental", CodeMap(HslocSystem, mapped: 1, unmapped: 0));

        Assert.Equal(5, Assert.Single(details.CodeMaps).MappedCount);
        Assert.Equal(2, details.Passes.Count);
    }

    #endregion

    private static NormalizationMappingDetails Record(
        NormalizationMappingDetails? stored,
        string queryType,
        params CodeMapOutcome[] outcomes) =>
        Record(stored, queryType, outcomes, "correlation-1");

    private static NormalizationMappingDetails Record(
        NormalizationMappingDetails? stored,
        string queryType,
        CodeMapOutcome outcome,
        string correlationId) =>
        Record(stored, queryType, [outcome], correlationId);

    private static NormalizationMappingDetails Record(
        NormalizationMappingDetails? stored,
        string queryType,
        IReadOnlyList<CodeMapOutcome> outcomes,
        string correlationId) =>
        CodeMapIndicator.Merge(stored, correlationId, queryType, outcomes);

    private static CodeMapOutcome CodeMap(
        string targetSystem,
        int mapped,
        int unmapped,
        string sourceSystem = LocalSystem,
        IReadOnlyList<string>? unmappedCodes = null) =>
        new(sourceSystem, targetSystem, MappingStatus.Mapped, mapped, unmapped, 0, unmappedCodes ?? []);
}
