using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using System.Text.Json;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
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
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    private const string LocalSystem = "http://hospital.example.org/locations";
    private const string Correlation = "correlation-1";

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

        await NormalizationAsync(scheduleId, DateTime.UtcNow, Hsloc(mapped: 0, unmapped: 2));

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
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Hsloc(mapped: 1, unmapped: 1));

        // Asserted against the persisted row rather than a tracked entity: ExecuteUpdateAsync bypasses the
        // change tracker, so an in-memory copy would not prove what the database holds.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Unmapped, row.EncounterMappingStatus);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, row.HslocMappingStatus);
        Assert.Single(CodeMapsIn(row.NormalizationDetails));
    }

    [Fact]
    public async Task AcquisitionAfterNormalization_LeavesTheNormalizationColumnsUntouched()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Hsloc(mapped: 2, unmapped: 0));
        await AcquisitionAsync(scheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "acquisition-json", DateTime.UtcNow);

        // The same guarantee in the other arrival order -- the one the retry topic can produce, since a
        // replayed message does not preserve partition affinity.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
        Assert.Single(CodeMapsIn(row.NormalizationDetails));
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

        await NormalizationAsync(scheduleId, DateTime.UtcNow, Hsloc(mapped: 2, unmapped: 0));
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
            NormalizationAsync(scheduleId, evaluatedAt, Hsloc(mapped: 2, unmapped: 0)));

        var row = Assert.Single(await AllRowsAsync(scheduleId));
        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
        Assert.Single(CodeMapsIn(row.NormalizationDetails));
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

    [Fact]
    public async Task CopyToSchedule_CarriesEveryColumnAndBothTimestampsOntoTheNewSchedule()
    {
        var sourceScheduleId = await SeedScheduleAsync();
        var targetScheduleId = await SeedScheduleAsync();

        var acquisitionAt = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);
        var normalizationAt = new DateTime(2026, 8, 20, 9, 5, 0, DateTimeKind.Utc);

        await AcquisitionAsync(sourceScheduleId, MappingIndicatorStatus.Assumed, MappingIndicatorStatus.Unmapped, "acquisition-json", acquisitionAt);
        await NormalizationAsync(sourceScheduleId, normalizationAt, Hsloc(mapped: 1, unmapped: 1));

        var copied = await CopyAsync(sourceScheduleId, targetScheduleId);

        Assert.Equal(1, copied);

        var row = await LoadAsync(targetScheduleId);
        Assert.Equal(PatientId, row.PatientId);
        Assert.Equal(MappingIndicatorStatus.Assumed, row.LocationOrgStatus);
        Assert.Equal(MappingIndicatorStatus.Unmapped, row.EncounterMappingStatus);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, row.HslocMappingStatus);
        Assert.Equal("acquisition-json", row.AcquisitionDetails);
        Assert.Single(CodeMapsIn(row.NormalizationDetails));

        // Carried verbatim rather than restamped: they record when the mapping was evaluated, and a
        // timestamp predating the new schedule is the signal that these came from the original run.
        Assert.Equal(acquisitionAt, row.AcquisitionEvaluatedAt);
        Assert.Equal(normalizationAt, row.NormalizationEvaluatedAt);
    }

    [Fact]
    public async Task CopyToSchedule_GivesTheNewRowsTheirOwnIdentity()
    {
        var sourceScheduleId = await SeedScheduleAsync();
        var targetScheduleId = await SeedScheduleAsync();

        await AcquisitionAsync(sourceScheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "acquisition-json", DateTime.UtcNow);
        await CopyAsync(sourceScheduleId, targetScheduleId);

        var original = await LoadAsync(sourceScheduleId);
        var copy = await LoadAsync(targetScheduleId);

        // Fresh primary key, new schedule, and the source row left untouched -- a regenerate must not
        // disturb the report it was generated from.
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(targetScheduleId, copy.ReportScheduleId);
        Assert.Equal(sourceScheduleId, original.ReportScheduleId);
    }

    [Fact]
    public async Task CopyToSchedule_SourceWithNoOutcomes_CopiesNothing()
    {
        var sourceScheduleId = await SeedScheduleAsync();
        var targetScheduleId = await SeedScheduleAsync();

        var copied = await CopyAsync(sourceScheduleId, targetScheduleId);

        // A report predating this feature has nothing to carry forward. Leaving the rows absent is correct;
        // inventing an outcome would claim the mapping was evaluated when it never was.
        Assert.Equal(0, copied);
        Assert.Empty(await AllRowsAsync(targetScheduleId));
    }

    [Fact]
    public async Task CopyToSchedule_RegenerateOfARegenerate_KeepsTheOriginalTimestamps()
    {
        var originalScheduleId = await SeedScheduleAsync();
        var firstRegenerateId = await SeedScheduleAsync();
        var secondRegenerateId = await SeedScheduleAsync();

        var acquisitionAt = new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc);
        await AcquisitionAsync(originalScheduleId, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "acquisition-json", acquisitionAt);

        await CopyAsync(originalScheduleId, firstRegenerateId);
        await CopyAsync(firstRegenerateId, secondRegenerateId);

        // Each hop copies from its immediate predecessor, so the chain must not drift the evaluation time
        // forward -- the resources being re-evaluated are still the ones the original acquisition wrote.
        Assert.Equal(acquisitionAt, (await LoadAsync(secondRegenerateId)).AcquisitionEvaluatedAt);
    }

    [Fact]
    public async Task CopyToSchedule_CopiesEveryPatientOnTheSourceSchedule()
    {
        var sourceScheduleId = await SeedScheduleAsync();
        var targetScheduleId = await SeedScheduleAsync();

        using (var scope = _scopeFactory.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();
            await manager.UpsertAcquisitionOutcomeAsync(FacilityId, sourceScheduleId, "patient-a", MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, null, DateTime.UtcNow);
            await manager.UpsertAcquisitionOutcomeAsync(FacilityId, sourceScheduleId, "patient-b", MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.Unmapped, null, DateTime.UtcNow);
        }

        var copied = await CopyAsync(sourceScheduleId, targetScheduleId);

        Assert.Equal(2, copied);
        var rows = await AllRowsAsync(targetScheduleId);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.PatientId == "patient-a" && row.LocationOrgStatus == MappingIndicatorStatus.Mapped);
        Assert.Contains(rows, row => row.PatientId == "patient-b" && row.LocationOrgStatus == MappingIndicatorStatus.Unmapped);
    }

    [Fact]
    public async Task RepeatedNormalizationPass_MergesWithWhatTheEarlierPassStored()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Correlation, "Initial",
            Hsloc(mapped: 2, unmapped: 0));
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Correlation, "Supplemental",
            Hsloc(mapped: 1, unmapped: 3, "PHARMACY"));

        // Unlike acquisition, a Normalization message is not the whole picture: it reports only the
        // resources of one acquisition pass, and a reportable patient goes through two. Overwriting would
        // report this patient on the strength of whichever pass landed last.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, row.HslocMappingStatus);

        var outcome = Assert.Single(CodeMapsIn(row.NormalizationDetails));
        Assert.Equal(3, outcome.MappedCount);
        Assert.Equal(3, outcome.UnmappedCount);
        Assert.Equal("PHARMACY", Assert.Single(outcome.UnmappedCodes));
    }

    [Fact]
    public async Task SupplementalPassReportingNothing_LeavesTheInitialPassIntact()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Correlation, "Initial",
            Hsloc(mapped: 1, unmapped: 0));
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Correlation, "Supplemental");

        // The concrete defect. A reportable patient's supplemental acquisition fetches no Location, so its
        // message carries an empty outcome list -- which a replacing write stores as NotApplicable, erasing
        // a patient whose locations did map.
        var row = await LoadAsync(scheduleId);
        Assert.Equal(MappingIndicatorStatus.Mapped, row.HslocMappingStatus);
        Assert.Equal(1, Assert.Single(CodeMapsIn(row.NormalizationDetails)).MappedCount);

        // The later timestamp still advances: the pass did run and did report.
        Assert.True(row.NormalizationEvaluatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task MergingIsScopedToOneSchedule()
    {
        var firstScheduleId = await SeedScheduleAsync();
        var secondScheduleId = await SeedScheduleAsync();

        await NormalizationAsync(firstScheduleId, DateTime.UtcNow, Hsloc(mapped: 4, unmapped: 0));
        await NormalizationAsync(secondScheduleId, DateTime.UtcNow, Hsloc(mapped: 1, unmapped: 0));

        // Re-running a report creates a new schedule, so merging within the row can only ever combine
        // passes of the same run. Without that, a re-acquisition would accumulate onto the old counts.
        Assert.Equal(1, Assert.Single(CodeMapsIn((await LoadAsync(secondScheduleId)).NormalizationDetails)).MappedCount);
    }

    [Fact]
    public async Task RedeliveringThePassAlreadyRecorded_LeavesTheTotalsUnchanged()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Correlation, "Initial",
            Hsloc(mapped: 2, unmapped: 1, "PHARMACY"));
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Correlation, "Initial",
            Hsloc(mapped: 2, unmapped: 1, "PHARMACY"));

        // Kafka is at-least-once and the offset is committed after this write, so a crash in between
        // redelivers the message. Combining by addition would count the patient's codes twice; the pass
        // identity makes the second write replace the first's contribution instead.
        var outcome = Assert.Single(CodeMapsIn((await LoadAsync(scheduleId)).NormalizationDetails));
        Assert.Equal(2, outcome.MappedCount);
        Assert.Equal(1, outcome.UnmappedCount);
    }

    [Fact]
    public async Task ARedeliveredPassCarryingMoreThanBefore_ReplacesRatherThanAdds()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Correlation, "Initial",
            Hsloc(mapped: 1, unmapped: 0));
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Correlation, "Initial",
            Hsloc(mapped: 5, unmapped: 2, "PHARMACY"));

        // A replay is not necessarily byte-identical -- a retried pass can get further than the attempt
        // that failed. The later report of a pass is the authoritative one for that pass.
        var outcome = Assert.Single(CodeMapsIn((await LoadAsync(scheduleId)).NormalizationDetails));
        Assert.Equal(5, outcome.MappedCount);
        Assert.Equal(2, outcome.UnmappedCount);
    }

    [Fact]
    public async Task EachPassIsRetainedSeparatelyAlongsideTheTotals()
    {
        var scheduleId = await SeedScheduleAsync();

        await NormalizationAsync(scheduleId, DateTime.UtcNow.AddMinutes(-5), Correlation, "Initial",
            Hsloc(mapped: 2, unmapped: 0));
        await NormalizationAsync(scheduleId, DateTime.UtcNow, Correlation, "Supplemental",
            Hsloc(mapped: 1, unmapped: 0));

        // The per-pass breakdown is what makes a redelivery replaceable, so it has to survive into storage
        // rather than being collapsed into the totals on write.
        var details = DetailsIn((await LoadAsync(scheduleId)).NormalizationDetails);
        Assert.Equal(2, details.Passes.Count);
        Assert.Single(details.Passes, pass => pass.QueryType == "Initial");
        Assert.Single(details.Passes, pass => pass.QueryType == "Supplemental");
        Assert.Equal(3, Assert.Single(details.CodeMaps).MappedCount);
    }

    [Fact]
    public async Task TwoPassesWritingConcurrently_BothSurvive()
    {
        var scheduleId = await SeedScheduleAsync();
        var evaluatedAt = DateTime.UtcNow;

        // Independent scopes so both can read the same stored value before either writes. Whether that
        // interleaving happens on a given run is not deterministic, so this asserts the outcome rather than
        // the branch: the compare-and-swap must make the loser re-read and recombine, never drop a pass.
        await Task.WhenAll(
            NormalizationAsync(scheduleId, evaluatedAt, Correlation, "Initial", Hsloc(mapped: 2, unmapped: 0)),
            NormalizationAsync(scheduleId, evaluatedAt, Correlation, "Supplemental", Hsloc(mapped: 1, unmapped: 0)));

        var details = DetailsIn((await LoadAsync(scheduleId)).NormalizationDetails);
        Assert.Equal(2, details.Passes.Count);
        Assert.Equal(3, Assert.Single(details.CodeMaps).MappedCount);
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

    private Task NormalizationAsync(
        Guid scheduleId,
        DateTime evaluatedAt,
        params CodeMapOutcome[] codeMapOutcomes) =>
        NormalizationAsync(scheduleId, evaluatedAt, "correlation-1", "Initial", codeMapOutcomes);

    private async Task NormalizationAsync(
        Guid scheduleId,
        DateTime evaluatedAt,
        string? correlationId,
        string? queryType,
        params CodeMapOutcome[] codeMapOutcomes)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();

        await manager.UpsertNormalizationOutcomeAsync(
            FacilityId, scheduleId, PatientId, correlationId, queryType, codeMapOutcomes, evaluatedAt);
    }

    private static CodeMapOutcome Hsloc(int mapped, int unmapped, params string[] unmappedCodes) =>
        new(LocalSystem, HslocSystem, MappingStatus.Mapped, mapped, unmapped, 0, unmappedCodes);

    private static NormalizationMappingDetails DetailsIn(string? normalizationDetails) =>
        JsonSerializer.Deserialize<NormalizationMappingDetails>(normalizationDetails!)!;

    private static IReadOnlyList<CodeMapOutcome> CodeMapsIn(string? normalizationDetails) =>
        DetailsIn(normalizationDetails).CodeMaps;

    private async Task<int> CopyAsync(Guid sourceScheduleId, Guid targetScheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();

        return await manager.CopyToScheduleAsync(sourceScheduleId, targetScheduleId);
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
