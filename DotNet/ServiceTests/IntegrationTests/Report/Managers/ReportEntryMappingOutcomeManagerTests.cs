using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Covers the upsert and, above all, column ownership: the two producers write disjoint groups, so
/// whichever arrives second must leave the first one's work byte-identical.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportEntryMappingOutcomeManagerTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";

    private readonly IServiceScopeFactory _scopeFactory;

    public ReportEntryMappingOutcomeManagerTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task UpsertAcquisitionOutcome_NoExistingRow_Inserts()
    {
        var scheduleId = await SeedScheduleAsync();
        var evaluatedAt = DateTime.UtcNow;

        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.PartiallyMapped, "{\"locationOrg\":{}}", evaluatedAt);

        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, row.EncounterMappingStatus);
        Assert.Equal("{\"locationOrg\":{}}", row.AcquisitionDetails);
        Assert.NotNull(row.AcquisitionEvaluatedAt);

        // The other source has said nothing, which must be distinguishable from having said "nothing to
        // report" -- hence NotEvaluated and a null timestamp rather than NotApplicable.
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, row.HslocMappingStatus);
        Assert.Null(row.NormalizationEvaluatedAt);
        Assert.Null(row.NormalizationDetails);
    }

    [Fact]
    public async Task UpsertNormalizationOutcome_NoExistingRow_Inserts()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, MappingIndicatorStatus.Unmapped, "{\"codeMaps\":[]}", DateTime.UtcNow);

        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Unmapped, row.HslocMappingStatus);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.NotEvaluated, row.EncounterMappingStatus);
        Assert.Null(row.AcquisitionEvaluatedAt);
    }

    [Fact]
    public async Task NormalizationAfterAcquisition_LeavesTheAcquisitionColumnsUntouched()
    {
        var scheduleId = await SeedScheduleAsync();
        var acquisitionAt = DateTime.UtcNow.AddMinutes(-5);

        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Assumed, MappingIndicatorStatus.Unmapped, "acquisition-json", acquisitionAt);
        await NormalizationAsync(scheduleId, MappingIndicatorStatus.PartiallyMapped, "normalization-json", DateTime.UtcNow);

        // Asserted against the persisted row rather than a tracked entity: ExecuteUpdateAsync bypasses the
        // change tracker, so an in-memory copy would not prove what the database holds.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Unmapped, row.EncounterMappingStatus);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, row.HslocMappingStatus);
        Assert.Equal("normalization-json", row.NormalizationDetails);
    }

    [Fact]
    public async Task AcquisitionAfterNormalization_LeavesTheNormalizationColumnsUntouched()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, MappingIndicatorStatus.Mapped, "normalization-json", DateTime.UtcNow.AddMinutes(-5));
        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "acquisition-json", DateTime.UtcNow);

        // The same guarantee in the other arrival order -- the one the retry topic can produce, since a
        // replayed message does not preserve partition affinity.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
        Assert.Equal("normalization-json", row.NormalizationDetails);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
    }

    [Fact]
    public async Task RepeatedAcquisitionPass_OverwritesItsOwnColumnsInPlace()
    {
        var scheduleId = await SeedScheduleAsync();

        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Unmapped, "first", DateTime.UtcNow.AddMinutes(-5));
        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "second", DateTime.UtcNow);

        // The row is current best knowledge for the pair, not a log. A patient can be re-acquired, and the
        // later pass sees the most resources.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.LocationOrgStatus);
        Assert.Equal("second", row.AcquisitionDetails);
        Assert.Single(await AllRowsAsync(scheduleId));
    }

    [Fact]
    public async Task BothSourcesForOnePair_ProduceOneRowCarryingBothColumnGroups()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, MappingIndicatorStatus.Mapped, "normalization-json", DateTime.UtcNow);
        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Assumed, MappingIndicatorStatus.Unmapped, "acquisition-json", DateTime.UtcNow);

        var row = Assert.Single(await AllRowsAsync(scheduleId));

        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
    }

    [Fact]
    public async Task BothSourcesArrivingTogether_StillProduceExactlyOneRow()
    {
        var scheduleId = await SeedScheduleAsync();
        var evaluatedAt = DateTime.UtcNow;

        // Run concurrently on independent scopes so both sources can find no row and both attempt an
        // insert. Whether that interleaving actually occurs on a given run is not deterministic -- the
        // update-then-insert window is real but narrow -- so this asserts the outcome rather than the
        // branch: whichever path is taken, the unique index must leave one row carrying both groups.
        await Task.WhenAll(
            AcquisitionAsync(scheduleId, MappingIndicatorStatus.Assumed, MappingIndicatorStatus.Unmapped, "acquisition-json", evaluatedAt),
            NormalizationAsync(scheduleId, MappingIndicatorStatus.Mapped, "normalization-json", evaluatedAt));

        var row = Assert.Single(await AllRowsAsync(scheduleId));
        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
        Assert.Equal("normalization-json", row.NormalizationDetails);
    }

    [Fact]
    public async Task TwoSchedulesForOnePatient_AreStoredSeparately()
    {
        var firstScheduleId = await SeedScheduleAsync();
        var secondScheduleId = await SeedScheduleAsync();

        await AcquisitionAsync(firstScheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "first", DateTime.UtcNow);
        await AcquisitionAsync(secondScheduleId, MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Unmapped, "second", DateTime.UtcNow);

        // One acquisition can serve several reporting periods, and each report keeps its own answer.
        Assert.Equal(MappingIndicatorStatus.Mapped, (await LoadAsync(firstScheduleId)).LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Unmapped, (await LoadAsync(secondScheduleId)).LocationOrgStatus);
    }

    [Fact]
    public async Task ModifyDateIsStampedExplicitly()
    {
        var scheduleId = await SeedScheduleAsync();
        var evaluatedAt = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, null, evaluatedAt);
        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Unmapped, null, evaluatedAt.AddHours(1));

        // ExecuteUpdateAsync never reaches the SaveChanges interceptor that normally stamps ModifyDate, so
        // the update has to set it itself or the column silently stops tracking the last write.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(evaluatedAt.AddHours(1), row.ModifyDate);
    }

    #region Helpers

    // Each call gets its own scope, and must await inside it: returning the task and letting the scope
    // dispose first closes the connection out from under the DbContext.
    private async Task AcquisitionAsync(
        Guid scheduleId,
        MappingIndicatorStatus locationOrgStatus,
        MappingIndicatorStatus encounterMappingStatus,
        string? details,
        DateTime evaluatedAt)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();

        await manager.UpsertAcquisitionOutcomeAsync(
            FacilityId, scheduleId, PatientId, locationOrgStatus, encounterMappingStatus, details, evaluatedAt);
    }

    private async Task NormalizationAsync(
        Guid scheduleId,
        MappingIndicatorStatus hslocMappingStatus,
        string? details,
        DateTime evaluatedAt)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();

        await manager.UpsertNormalizationOutcomeAsync(
            FacilityId, scheduleId, PatientId, hslocMappingStatus, details, evaluatedAt);
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

    private async Task<ReportEntryMappingOutcome> LoadAsync(Guid scheduleId) =>
        Assert.Single(await AllRowsAsync(scheduleId));

    private async Task<List<ReportEntryMappingOutcome>> AllRowsAsync(Guid scheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        return await dbContext.ReportEntryMappingOutcome
            .AsNoTracking()
            .Where(outcome => outcome.ReportScheduleId == scheduleId)
            .ToListAsync();
    }

    #endregion
}
