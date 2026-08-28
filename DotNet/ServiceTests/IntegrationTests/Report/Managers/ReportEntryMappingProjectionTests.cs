using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Covers how the stored mapping outcome reaches the API: the left join onto the report entry, and the
/// per-patient drill-down that carries the evidence.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportEntryMappingProjectionTests
{
    private const string FacilityId = "projection-facility";
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    private const string LocalSystem = "http://hospital.example.org/locations";

    private readonly IServiceScopeFactory _scopeFactory;

    public ReportEntryMappingProjectionTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    #region Search projection

    [Fact]
    public async Task PatientWithNoOutcomeRow_IsStillReturned_AsNotEvaluated()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "no-outcome");

        var entry = Assert.Single((await SearchAsync(scheduleId)).Records);

        // The join has to be a LEFT join. An inner one would drop this patient from the grid entirely --
        // which is every patient of every report that predates this feature.
        Assert.Equal("no-outcome", entry.PatientId);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, entry.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, entry.EncounterMappingStatus);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, entry.HslocMappingStatus);
        Assert.Null(entry.AcquisitionEvaluatedAt);
        Assert.Null(entry.NormalizationEvaluatedAt);
    }

    [Fact]
    public async Task IndicatorsAndTimestampsReachTheGrid()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "mapped");
        await SeedOutcomeAsync(scheduleId, "mapped",
            MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.PartiallyMapped,
            acquisitionAt: DateTime.UtcNow.AddMinutes(-5), normalizationAt: DateTime.UtcNow);

        var entry = Assert.Single((await SearchAsync(scheduleId)).Records);

        Assert.Equal(MappingIndicatorStatus.Mapped, entry.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, entry.HslocMappingStatus);
        Assert.NotNull(entry.AcquisitionEvaluatedAt);
        Assert.NotNull(entry.NormalizationEvaluatedAt);
    }

    [Fact]
    public async Task AssumedSurvivesToTheGrid_RatherThanCollapsingToMapped()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "assumed");
        await SeedOutcomeAsync(scheduleId, "assumed",
            MappingIndicatorStatus.Assumed, MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.NothingToEvaluate,
            acquisitionAt: DateTime.UtcNow, normalizationAt: DateTime.UtcNow);

        var entry = Assert.Single((await SearchAsync(scheduleId)).Records);

        // Assumed is the whole point of the column: this patient's org membership was never verified,
        // and reporting it as Mapped would hide exactly the case the indicator exists to surface.
        Assert.Equal(MappingIndicatorStatus.Assumed, entry.LocationOrgStatus);
    }

    [Fact]
    public async Task PatientStrippedAsNonOrg_ReportsExcludedRatherThanNotEvaluated()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "non-org");
        await SeedOutcomeAsync(scheduleId, "non-org",
            MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.NotEvaluated,
            acquisitionAt: DateTime.UtcNow, normalizationAt: null);

        var entry = Assert.Single((await SearchAsync(scheduleId)).Records);

        // No encounter belonged to the organization, so the patient is not in the report. Derived on read,
        // so the write path keeps its rule that each producer touches only its own columns.
        Assert.Equal(MappingIndicatorStatus.Excluded, entry.HslocMappingStatus);
        Assert.Null(entry.NormalizationEvaluatedAt);
    }

    [Fact]
    public async Task NonOrgPatientWhoseLocationsDidMap_IsStillExcluded()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "non-org-mapped");
        await SeedOutcomeAsync(scheduleId, "non-org-mapped",
            MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped,
            acquisitionAt: DateTime.UtcNow, normalizationAt: DateTime.UtcNow);

        var entry = Assert.Single((await SearchAsync(scheduleId)).Records);

        // The real case, not a contrived one. Stripping the patient's encounters leaves the Location
        // resources they referenced in the cache, so Normalization code maps them and genuinely reports
        // Mapped -- for a patient the report does not evaluate. Reporting that would read as a clean pass
        // and describe a location no measure ever sees.
        Assert.Equal(MappingIndicatorStatus.Excluded, entry.HslocMappingStatus);
    }

    [Fact]
    public async Task OneScheduleDoesNotSeeAnotherSchedulesOutcome()
    {
        var firstScheduleId = await SeedScheduleAsync();
        var secondScheduleId = await SeedScheduleAsync();

        await SeedEntryAsync(firstScheduleId, "shared-patient");
        await SeedEntryAsync(secondScheduleId, "shared-patient");
        await SeedOutcomeAsync(firstScheduleId, "shared-patient",
            MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped,
            acquisitionAt: DateTime.UtcNow, normalizationAt: DateTime.UtcNow);

        // The join is on (ReportScheduleId, PatientId), not on patient alone. One patient reported across
        // two periods must not have one report's answer bleed into the other's.
        var second = Assert.Single((await SearchAsync(secondScheduleId)).Records);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, second.LocationOrgStatus);
    }

    #endregion

    #region Per-patient detail

    [Fact]
    public async Task Detail_CarriesBothBlobsAsTypedModels()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "detailed");
        await SeedOutcomeAsync(scheduleId, "detailed",
            MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.PartiallyMapped,
            acquisitionAt: DateTime.UtcNow, normalizationAt: DateTime.UtcNow,
            acquisitionDetails: AcquisitionJson(), normalizationDetails: NormalizationJson());

        var detail = await DetailAsync(scheduleId, "detailed");

        // nvarchar(max) is the storage form; the client binds to fields rather than parsing a string.
        Assert.NotNull(detail!.Acquisition);
        Assert.Equal(3, detail.Acquisition!.LocationOrg.EncounterCount);
        Assert.Equal("loc-2", Assert.Single(detail.Acquisition.LocationOrg.Matches).LocationId);

        Assert.NotNull(detail.Normalization);
        var codeMap = Assert.Single(detail.Normalization!.CodeMaps);
        Assert.Equal(HslocSystem, codeMap.TargetSystem);
        Assert.Equal("PHARMACY", Assert.Single(codeMap.UnmappedCodes));
    }

    [Fact]
    public async Task Detail_SourceThatNeverReported_IsAbsentNotEmpty()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "half-reported");
        await SeedOutcomeAsync(scheduleId, "half-reported",
            MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.NotEvaluated,
            acquisitionAt: DateTime.UtcNow, normalizationAt: null,
            acquisitionDetails: AcquisitionJson());

        var detail = await DetailAsync(scheduleId, "half-reported");

        // An empty object would claim Normalization ran and found nothing. It has not run at all, and the
        // difference is the whole reason both timestamps are stored.
        Assert.NotNull(detail!.Acquisition);
        Assert.Null(detail.Normalization);
    }

    [Fact]
    public async Task Detail_NoOutcomeRow_Returns200WithNotEvaluated_NotNull()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "bare");

        var detail = await DetailAsync(scheduleId, "bare");

        // The report entry exists; only its mapping outcome is missing. Returning null here would make the
        // controller answer 404 and deny the entry itself.
        Assert.NotNull(detail);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, detail!.HslocMappingStatus);
        Assert.Null(detail.Acquisition);
        Assert.Null(detail.Normalization);
    }

    [Fact]
    public async Task Detail_UnreadableBlob_IsReportedAsAbsentRatherThanThrowing()
    {
        var scheduleId = await SeedScheduleAsync();
        await SeedEntryAsync(scheduleId, "corrupt");
        await SeedOutcomeAsync(scheduleId, "corrupt",
            MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped,
            acquisitionAt: DateTime.UtcNow, normalizationAt: DateTime.UtcNow,
            acquisitionDetails: "{ not json",
            normalizationDetails: NormalizationJson());

        var detail = await DetailAsync(scheduleId, "corrupt");

        // One unreadable blob must not take the whole entry down with it -- the indicators and the other
        // source are still perfectly good.
        Assert.NotNull(detail);
        Assert.Null(detail!.Acquisition);
        Assert.NotNull(detail.Normalization);
        Assert.Equal(MappingIndicatorStatus.Mapped, detail.HslocMappingStatus);
    }

    [Fact]
    public async Task Detail_MissingEntry_IsNull()
    {
        var scheduleId = await SeedScheduleAsync();

        // No entry at all is the case that legitimately becomes a 404.
        Assert.Null(await DetailAsync(scheduleId, "never-existed"));
    }

    #endregion

    #region Helpers

    private static string AcquisitionJson() =>
        "{\"LocationOrg\":{\"Status\":1,\"EncounterCount\":3,\"OrgEncounterCount\":2,\"AssumedOrgEncounterCount\":0,"
        + "\"Matches\":[{\"LocationId\":\"loc-2\",\"LocationName\":\"Radiology\",\"LocationAlias\":\"Radiology\","
        + "\"PartOfValue\":\"loc-root\",\"IsOrgLocation\":false}]}}";

    private static string NormalizationJson() =>
        "{\"CodeMaps\":[{\"SourceSystem\":\"" + LocalSystem + "\",\"TargetSystem\":\"" + HslocSystem + "\","
        + "\"Status\":2,\"MappedCount\":1,\"UnmappedCount\":1,\"FailureCount\":0,\"UnmappedCodes\":[\"PHARMACY\"]}],"
        + "\"Passes\":[{\"CorrelationId\":\"c1\",\"QueryType\":\"Initial\",\"CodeMaps\":[]}]}";

    private async Task<PagedConfigModelRecords> SearchAsync(Guid scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var result = await manager.SearchAsync(
            FacilityId, null, scheduleId, null, null, false, null, null, null, 25, 1);

        return new PagedConfigModelRecords(result.Records);
    }

    private sealed record PagedConfigModelRecords(List<ReportEntryModel> Records);

    private async Task<ReportEntryDetailModel?> DetailAsync(Guid scheduleId, string patientId)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        return await manager.GetEntryDetail(scheduleId, patientId);
    }

    private async Task<Guid> SeedScheduleAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-1),
            ReportEndDate = DateTimeOffset.UtcNow,
            Frequency = Frequency.Monthly,
            Status = ScheduleStatus.New,
            CreateDate = DateTime.UtcNow
        };

        dbContext.ReportSchedule.Add(schedule);
        await dbContext.SaveChangesAsync();

        return schedule.Id;
    }

    private async Task SeedEntryAsync(Guid scheduleId, string patientId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        dbContext.ReportEntry.Add(new ReportEntry
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ReportScheduleId = scheduleId,
            PatientId = patientId,
            ReportingStatus = ReportingStatus.PatientIdentified,
            CreateDate = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedOutcomeAsync(
        Guid scheduleId,
        string patientId,
        MappingIndicatorStatus locationOrg,
        MappingIndicatorStatus encounterMapping,
        MappingIndicatorStatus hsloc,
        DateTime? acquisitionAt,
        DateTime? normalizationAt,
        string? acquisitionDetails = null,
        string? normalizationDetails = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        dbContext.ReportEntryMappingOutcome.Add(new ReportEntryMappingOutcome
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ReportScheduleId = scheduleId,
            PatientId = patientId,
            LocationOrgStatus = locationOrg,
            EncounterMappingStatus = encounterMapping,
            HslocMappingStatus = hsloc,
            AcquisitionDetails = acquisitionDetails,
            AcquisitionEvaluatedAt = acquisitionAt,
            NormalizationDetails = normalizationDetails,
            NormalizationEvaluatedAt = normalizationAt,
            CreateDate = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    #endregion
}
