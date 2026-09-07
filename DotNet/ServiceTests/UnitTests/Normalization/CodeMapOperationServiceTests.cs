using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// Covers the per-code-map counts the service reports alongside the rewrite it performs.
/// </summary>
[Trait("Category", "UnitTests")]
public class CodeMapOperationServiceTests
{
    private const string LocalSystem = "http://hospital.example.org/locations";
    private const string OtherLocalSystem = "urn:oid:1.2.840.114350.1.13.277.3.7.2.686990";
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    private readonly CodeMapOperationService _service =
        new(new Mock<ILogger<CodeMapOperationService>>().Object);

    [Fact]
    public async Task AllCodingsMapped_ReportsMappedCountAndNoUnmapped()
    {
        var location = LocationWithTypeCodes(LocalSystem, "ICU", "ER");
        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4"), ("ER", "1108-0")));

        var result = await _service.ProcessOperationAsync(operation, location);

        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(LocalSystem, outcome.SourceSystem);
        Assert.Equal(HslocSystem, outcome.TargetSystem);
        Assert.Equal(2, outcome.MappedCount);
        Assert.Equal(0, outcome.UnmappedCount);
        Assert.Empty(outcome.UnmappedCodes);
    }

    [Fact]
    public async Task SomeCodingsUnmapped_ReportsBothCountsAndNamesTheMissingCodes()
    {
        var location = LocationWithTypeCodes(LocalSystem, "ICU", "ER", "PHARMACY");
        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // Partially applied still reports Success -- the status alone cannot distinguish 1-of-3 from 3-of-3,
        // which is the gap these counts exist to close.
        Assert.Equal(OperationStatus.Success, result.SuccessCode);
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(1, outcome.MappedCount);
        Assert.Equal(2, outcome.UnmappedCount);
        Assert.Equal(["ER", "PHARMACY"], outcome.UnmappedCodes.OrderBy(code => code));
    }

    [Fact]
    public async Task NoCodingMapped_ReturnsNoActionCarryingTheCounts()
    {
        var location = LocationWithTypeCodes(LocalSystem, "ICU", "ER");
        var operation = Operation(Map(LocalSystem, HslocSystem, ("SOMETHING-ELSE", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // The fully-unmapped case is the one a report most needs to surface, and it comes back as NoAction.
        // Dropping counts on NoAction would hide it behind the same silence as an operation that never ran.
        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(0, outcome.MappedCount);
        Assert.Equal(2, outcome.UnmappedCount);
    }

    [Fact]
    public async Task NothingAtTheFhirPath_ReturnsNoActionWithNoOutcomes()
    {
        var location = new Location { Id = "loc-1" };
        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // No coding was examined, so the map was never exercised. That is distinct from a map that ran and
        // found nothing -- reporting zero counts here would claim the facility's configuration was tested.
        Assert.Equal(OperationStatus.NoAction, result.SuccessCode);
        Assert.Null(result.CodeMapping);
    }

    [Fact]
    public async Task MapWhoseSourceSystemIsUnused_ProducesNoOutcome()
    {
        var location = LocationWithTypeCodes(LocalSystem, "ICU");
        var operation = Operation(
            Map(LocalSystem, HslocSystem, ("ICU", "1027-4")),
            Map(OtherLocalSystem, HslocSystem, ("BED", "1160-1")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // Absent, not zero: a map no coding used says nothing about the facility's configuration, whereas a
        // map that matched and found no code is a real gap.
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(LocalSystem, outcome.SourceSystem);
    }

    [Fact]
    public async Task TwoMapsWithDifferentTargets_AreReportedSeparately()
    {
        var location = new Location { Id = "loc-1" };
        location.Type.Add(new CodeableConcept { Coding = { new Coding(LocalSystem, "ICU") } });
        location.Type.Add(new CodeableConcept { Coding = { new Coding(OtherLocalSystem, "BED") } });

        var operation = Operation(
            Map(LocalSystem, HslocSystem, ("ICU", "1027-4")),
            Map(OtherLocalSystem, OtherLocalSystem, ("BED", "BED-NORMALIZED")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // A facility may map a Location code to something that is not HSLOC. Merging the tallies would make
        // the report attribute one map's result to the other.
        Assert.Equal(2, result.CodeMapping.Count);
        Assert.Single(result.CodeMapping, outcome => outcome.TargetSystem == HslocSystem);
        Assert.Single(result.CodeMapping, outcome => outcome.TargetSystem == OtherLocalSystem);
    }

    [Fact]
    public async Task TwoSourceSystemsIntoOneTarget_StayDistinct()
    {
        var location = new Location { Id = "loc-1" };
        location.Type.Add(new CodeableConcept { Coding = { new Coding(LocalSystem, "ICU") } });
        location.Type.Add(new CodeableConcept { Coding = { new Coding(OtherLocalSystem, "MISSING") } });

        var operation = Operation(
            Map(LocalSystem, HslocSystem, ("ICU", "1027-4")),
            Map(OtherLocalSystem, HslocSystem, ("BED", "1160-1")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // Without the source system these merge into one tally and the failing source cannot be identified.
        Assert.Equal(2, result.CodeMapping.Count);
        var mapped = Assert.Single(result.CodeMapping, outcome => outcome.SourceSystem == LocalSystem);
        Assert.Equal(1, mapped.MappedCount);

        var unmapped = Assert.Single(result.CodeMapping, outcome => outcome.SourceSystem == OtherLocalSystem);
        Assert.Equal(0, unmapped.MappedCount);
        Assert.Equal(1, unmapped.UnmappedCount);
        Assert.Equal("MISSING", Assert.Single(unmapped.UnmappedCodes));
    }

    [Fact]
    public async Task RepeatedUnmappedCode_CountsEveryOccurrenceButListsItOnce()
    {
        var location = LocationWithTypeCodes(LocalSystem, "PHARMACY", "PHARMACY", "PHARMACY");
        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // UnmappedCount is the true total; UnmappedCodes answers "what do I need to configure", where a
        // repeat adds nothing.
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(3, outcome.UnmappedCount);
        Assert.Equal("PHARMACY", Assert.Single(outcome.UnmappedCodes));
    }

    [Fact]
    public async Task UnmappedCodesAreRecordedAsTheEhrSentThem()
    {
        var location = LocationWithTypeCodes(LocalSystem, "ICU", "PHARMACY");
        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // The mapped coding is rewritten in place, so a code read back off the resource afterwards is the
        // target code. The unmapped list must hold the source code the facility would go and map.
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal("PHARMACY", Assert.Single(outcome.UnmappedCodes));
        Assert.DoesNotContain("1027-4", outcome.UnmappedCodes);
    }

    [Fact]
    public async Task CountsSpanEveryCodingInTheResource()
    {
        var location = new Location { Id = "loc-1" };
        location.Type.Add(new CodeableConcept
        {
            Coding =
            {
                new Coding(LocalSystem, "ICU"),
                new Coding(LocalSystem, "ER")
            }
        });
        location.Type.Add(new CodeableConcept { Coding = { new Coding(LocalSystem, "PHARMACY") } });

        var operation = Operation(Map(LocalSystem, HslocSystem, ("ICU", "1027-4"), ("ER", "1108-0")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // One tally per map across the whole resource, not one per CodeableConcept.
        var outcome = Assert.Single(result.CodeMapping);
        Assert.Equal(2, outcome.MappedCount);
        Assert.Equal(1, outcome.UnmappedCount);
    }

    [Fact]
    public async Task FailedOperation_ReportsNoCounts()
    {
        // A null CodeSystemMaps collection makes the operation throw, which BaseOperationService turns into
        // a Failure.
        var operation = new CodeMapOperation("Broken map", "type.coding", null!);
        var location = LocationWithTypeCodes(LocalSystem, "ICU");

        var result = await _service.ProcessOperationAsync(operation, location);

        // Reporting the pair as unmapped would blame the facility's configuration for a processing fault,
        // and reporting it as mapped would hide the fault entirely.
        Assert.Equal(OperationStatus.Failure, result.SuccessCode);
        Assert.Null(result.CodeMapping);
    }

    [Fact]
    public async Task ChainedMaps_ApplyInSequence_BecauseTheFilterIsEvaluatedAgainstTheRewrittenSystem()
    {
        // Characterization, not endorsement. UpdateCoding enumerates
        // codeSystemMaps.Where(x => x.SourceSystem == coding.System) lazily while the loop body rewrites
        // coding.System, so once the first map matches, later maps are tested against the NEW system. With
        // one map per source system this is invisible; with a chain it silently applies both.
        //
        // Pinned so the behavior is deliberate rather than discovered, and so the counts below are not
        // later blamed for it: each map is credited with the rewrite it actually performed.
        const string intermediateSystem = "http://example.org/intermediate";

        var location = LocationWithTypeCodes(LocalSystem, "ICU");
        var operation = Operation(
            Map(LocalSystem, intermediateSystem, ("ICU", "INT-1")),
            Map(intermediateSystem, HslocSystem, ("INT-1", "1027-4")));

        var result = await _service.ProcessOperationAsync(operation, location);

        // The coding went all the way through both maps in a single pass.
        var coding = location.Type.Single().Coding.Single();
        Assert.Equal(HslocSystem, coding.System);
        Assert.Equal("1027-4", coding.Code);

        // Both maps report one mapped coding: each did perform a rewrite, so neither count is misleading
        // on its own -- but summing them would double-count a single coding.
        Assert.Equal(2, result.CodeMapping.Count);
        Assert.All(result.CodeMapping, outcome => Assert.Equal(1, outcome.MappedCount));
    }

    private static Location LocationWithTypeCodes(string system, params string[] codes)
    {
        var location = new Location { Id = "loc-1" };
        foreach (var code in codes)
        {
            location.Type.Add(new CodeableConcept { Coding = { new Coding(system, code) } });
        }

        return location;
    }

    private static CodeSystemMap Map(string sourceSystem, string targetSystem, params (string Source, string Target)[] codes) =>
        new(sourceSystem, targetSystem, codes.ToDictionary(
            pair => pair.Source,
            pair => new CodeMap(pair.Target, $"Display for {pair.Target}")));

    private static CodeMapOperation Operation(params CodeSystemMap[] maps) =>
        new("Location type to HSLOC", "type.coding", maps.ToList());
}
