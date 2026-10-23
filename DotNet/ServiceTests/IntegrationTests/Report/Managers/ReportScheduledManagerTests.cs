using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportScheduledManagerTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportScheduledManagerTests(ReportIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    #region Argument Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateReportsDeletedStatusForFacility_WithNullOrWhitespaceFacilityId_ThrowsArgumentException(string? facilityId)
    {
        using var scope = _scopeFactory.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var act = async () => await sut.UpdateReportsDeletedStatusForFacility(facilityId!, true);

        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Equal("facilityId", exception.ParamName);
    }

    #endregion

    #region Soft Delete (deleted = true) Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_SetsIsDeletedTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.Submitted);

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.True(updated!.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_SetsModifyDate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.Submitted);
        var beforeTest = DateTime.UtcNow;

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.NotNull(updated!.ModifyDate);
        Assert.True(updated.ModifyDate >= beforeTest);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_SetsIsDeletedTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.Scheduled);

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.True(updated!.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithEndOfPeriodReport_WhenDeleted_LeavesReportUntouched()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var originalModifyDate = report.ModifyDate;

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.False(updated!.IsDeleted);
        Assert.Equal(originalModifyDate, updated.ModifyDate);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithNewReport_WhenDeleted_LeavesReportUntouched()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.New, isDeleted: false);
        var originalModifyDate = report.ModifyDate;

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.False(updated!.IsDeleted);
        Assert.Equal(originalModifyDate, updated.ModifyDate);
    }

    #endregion

    #region Restore (deleted = false) Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenNotDeleted_SetsIsDeletedFalse()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: true);

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.False(updated!.IsDeleted);
    }

    #endregion

    #region Mixed Status & SaveChanges Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMixedStatuses_WhenDeleted_HandlesEachStatusCorrectly()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var newReport = CreateReport(facilityId, ScheduleStatus.New, isDeleted: false);

        await SeedAsync(context, scheduledReport, endOfPeriodReport, newReport);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updatedScheduled = await context.ReportSchedule.FindAsync(scheduledReport.Id);
        var updatedEndOfPeriod = await context.ReportSchedule.FindAsync(endOfPeriodReport.Id);
        var updatedNew = await context.ReportSchedule.FindAsync(newReport.Id);

        Assert.True(updatedScheduled!.IsDeleted);
        Assert.False(updatedEndOfPeriod!.IsDeleted);
        Assert.False(updatedNew!.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMultipleSubmittedReports_UpdatesAllAndSavesOnce()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report1 = CreateReport(facilityId, ScheduleStatus.Submitted);
        var report2 = CreateReport(facilityId, ScheduleStatus.Submitted);

        await SeedAsync(context, report1, report2);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated1 = await context.ReportSchedule.FindAsync(report1.Id);
        var updated2 = await context.ReportSchedule.FindAsync(report2.Id);

        Assert.True(updated1!.IsDeleted);
        Assert.True(updated2!.IsDeleted);
    }

    #endregion

    #region Summary Tests

    [Fact]
    public async Task GetReportSummaries_WithMultipleReportTypes_ReturnsScheduleLevelCounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.Scheduled);
        report.ReportTypes.Add(new ScheduleReportType { ReportType = "type-a" });
        report.ReportTypes.Add(new ScheduleReportType { ReportType = "type-b" });
        report.ReportEntries.Add(new ReportEntry
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            PatientId = "patient-a",
            CreateDate = DateTime.UtcNow
        });
        report.ReportEntries.Add(new ReportEntry
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            PatientId = "patient-b",
            CreateDate = DateTime.UtcNow
        });
        report.ReportPopulations.Add(CreateReportPopulation(facilityId, "type-a", 2));
        report.ReportPopulations.Add(CreateReportPopulation(facilityId, "type-b", 1));

        await SeedAsync(context, report);

        var result = await sut.GetReportSummaries(facilityId, null, null, null, 10, 1);

        var summary = Assert.Single(result.Records);
        Assert.Equal(report.Id, Guid.Parse(summary.ReportScheduleId));
        Assert.Equal(["type-a", "type-b"], summary.ReportTypes);
        Assert.Equal(2, summary.PatientCount);
        Assert.Equal(3, summary.InitialPopulationCount);
    }

    #endregion

    #region Helper Methods

    #region Bypassed submission (LEGLINK-1083)

    /// <summary>
    /// Pins the EF behaviour the whole feature rests on. EnableSubmission is configured with
    /// HasDefaultValue(true), which makes EF infer ValueGeneratedOnAdd on top of the column's
    /// DEFAULT 1 constraint -- the classic shape of a bool that silently reverts to its store
    /// default on insert.
    ///
    /// It does not revert, because since EF Core 7 HasDefaultValue(v) also sets the property's
    /// sentinel to v: the value omitted from the INSERT is true, so an explicit false is sent.
    /// Nothing else covers this, and it is subtle enough that an EF upgrade or an edit to that
    /// one line could flip it without any other test noticing -- at which point bypassSubmission
    /// would be accepted, appear to work, and submit anyway.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddAsync_PersistsEnableSubmissionExactlyAsGiven(bool enableSubmission)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var model = new ReportScheduleModel
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Adhoc,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.New,
            EnableSubmission = enableSubmission,
            CreateDate = DateTime.UtcNow
        };

        await sut.AddAsync(model, CancellationToken.None);

        // Read through the raw entity rather than the manager, so a projection that happened to
        // default the value could not mask a bad write.
        context.ChangeTracker.Clear();
        var stored = await context.ReportSchedule.FindAsync(model.Id);

        Assert.NotNull(stored);
        Assert.Equal(enableSubmission, stored.EnableSubmission);
    }


    /// <summary>
    /// A bypassed report completed its work, so the facility-facing status is Completed --
    /// the same value a submitted report reads. LEGLINK-1009 defines that vocabulary as
    /// exactly Pending, Completed and Canceled, projected over the internal state machine
    /// rather than mirroring it, so no fourth value is added for the bypass.
    /// </summary>
    [Fact]
    public async Task GetReportSummaries_WithCompletedNotSubmittedReport_ProjectsToCompleted()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted));

        var result = await sut.GetReportSummaries(facilityId, null, null, null, 10, 1);

        var summary = Assert.Single(result.Records);
        Assert.Equal(ReportStatus.Completed, summary.Status);
    }

    /// <summary>
    /// The projection's else branch falls through to Unknown, and every status declared
    /// before this ticket was named explicitly, so CompletedNotSubmitted is the first value
    /// that could ever reach it. Landing there would read as a bug rather than a decision.
    /// </summary>
    [Fact]
    public async Task GetReportSummaries_WithCompletedNotSubmittedReport_DoesNotProjectToUnknown()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted));

        var result = await sut.GetReportSummaries(facilityId, null, null, null, 10, 1);

        Assert.NotEqual(ReportStatus.Unknown, Assert.Single(result.Records).Status);
    }

    /// <summary>
    /// Filtering by Completed has to return bypassed reports alongside submitted ones,
    /// otherwise a bypassed report is reachable by no status filter at all.
    /// </summary>
    [Fact]
    public async Task GetReportSummaries_FilteredByCompleted_ReturnsBothSubmittedAndBypassed()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.Submitted));
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted));
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.EndOfPeriod));

        var result = await sut.GetReportSummaries(facilityId, ReportStatus.Completed, null, null, 10, 1);

        Assert.Equal(2, result.Records.Count);
    }

    /// <summary>
    /// Canceled is derived from IsDeleted and is evaluated first, so it still wins over a
    /// terminal status.
    /// </summary>
    [Fact]
    public async Task GetReportSummaries_WithDeletedCompletedNotSubmittedReport_ProjectsToCanceled()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        await SeedAsync(context, CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted, isDeleted: true));

        var result = await sut.GetReportSummaries(facilityId, null, null, null, 10, 1);

        Assert.Equal(ReportStatus.Canceled, Assert.Single(result.Records).Status);
    }

    /// <summary>
    /// Bulk soft-delete by facility only ever batched Submitted rows. Without the terminal
    /// set a bypassed report would be undeletable through this path.
    /// </summary>
    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithCompletedNotSubmittedReport_SetsIsDeletedTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted);

        await SeedAsync(context, report);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.True(updated!.IsDeleted);
    }

    /// <summary>
    /// Soft-delete by id refuses only reports still in progress. A bypassed report has
    /// finished, so it must be deletable.
    /// </summary>
    [Fact]
    public async Task SoftDeleteByReportTrackingIdAsync_WithCompletedNotSubmittedReport_Succeeds()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var report = CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted);

        await SeedAsync(context, report);

        await sut.SoftDeleteByReportTrackingIdAsync(report.Id);

        context.ChangeTracker.Clear();
        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.True(updated!.IsDeleted);
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    public async Task SoftDeleteByReportTrackingIdAsync_WithInProgressReport_Throws(ScheduleStatus status)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status);
        await SeedAsync(context, report);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SoftDeleteByReportTrackingIdAsync(report.Id));

        Assert.Contains("currently in progress", ex.Message);
        context.ChangeTracker.Clear();
        Assert.False((await context.ReportSchedule.FindAsync(report.Id))!.IsDeleted);
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    public async Task SoftDeleteByReportTrackingIdAsync_WithInProgressReport_AllowInProgress_Succeeds(ScheduleStatus status)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status);
        await SeedAsync(context, report);

        await sut.SoftDeleteByReportTrackingIdAsync(report.Id, allowInProgress: true);

        context.ChangeTracker.Clear();
        Assert.True((await context.ReportSchedule.FindAsync(report.Id))!.IsDeleted);
    }

    [Theory]
    [InlineData(ScheduleStatus.Scheduled)]
    [InlineData(ScheduleStatus.Submitted)]
    public async Task RestoreByReportTrackingIdAsync_WithRestorableStatus_ClearsIsDeleted(ScheduleStatus status)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status, isDeleted: true);
        await SeedAsync(context, report);

        await sut.RestoreByReportTrackingIdAsync(report.Id);

        context.ChangeTracker.Clear();
        Assert.False((await context.ReportSchedule.FindAsync(report.Id))!.IsDeleted);
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public async Task RestoreByReportTrackingIdAsync_WithNonRestorableStatus_ThrowsAndStaysDeleted(ScheduleStatus status)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status, isDeleted: true);
        await SeedAsync(context, report);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RestoreByReportTrackingIdAsync(report.Id));

        Assert.Contains("cannot be restored", ex.Message);
        Assert.Contains(status.ToString(), ex.Message);
        context.ChangeTracker.Clear();
        Assert.True((await context.ReportSchedule.FindAsync(report.Id))!.IsDeleted);
    }

    #endregion

    #region Retention dates (LEGLINK-1245)

    /// <summary>
    /// The first write of a terminal status stamps CompletedDate. A later write with the same
    /// terminal status, which is what a regenerate produces, leaves it where it was.
    /// </summary>
    [Theory]
    [InlineData(ScheduleStatus.Submitted)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public async Task UpdateAsync_StatusBecomesTerminal_SetsCompletedDateOnce(ScheduleStatus terminalStatus)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), ScheduleStatus.EndOfPeriod);
        await SeedAsync(context, report);

        var before = DateTimeOffset.UtcNow;
        await sut.UpdateAsync(ToModel(report, terminalStatus), CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        context.ChangeTracker.Clear();
        var first = (await context.ReportSchedule.FindAsync(report.Id))!.CompletedDate;
        Assert.NotNull(first);
        Assert.InRange(first.Value, before, after);
        Assert.Equal(TimeSpan.Zero, first.Value.Offset);

        await Task.Delay(20);
        await sut.UpdateAsync(ToModel(report, terminalStatus), CancellationToken.None);

        context.ChangeTracker.Clear();
        Assert.Equal(first, (await context.ReportSchedule.FindAsync(report.Id))!.CompletedDate);
    }

    [Fact]
    public async Task UpdateAsync_StatusStaysInProgress_LeavesCompletedDateNull()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), ScheduleStatus.Scheduled);
        await SeedAsync(context, report);

        await sut.UpdateAsync(ToModel(report, ScheduleStatus.EndOfPeriod), CancellationToken.None);

        context.ChangeTracker.Clear();
        Assert.Null((await context.ReportSchedule.FindAsync(report.Id))!.CompletedDate);
    }

    [Theory]
    [InlineData(ScheduleStatus.Scheduled, false)]
    [InlineData(ScheduleStatus.New, true)]
    [InlineData(ScheduleStatus.EndOfPeriod, true)]
    public async Task SoftDeleteByReportTrackingIdAsync_UnfinishedReport_SetsSoftDeletedDate(ScheduleStatus status, bool allowInProgress)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status);
        await SeedAsync(context, report);

        var before = DateTimeOffset.UtcNow;
        await sut.SoftDeleteByReportTrackingIdAsync(report.Id, allowInProgress: allowInProgress);
        var after = DateTimeOffset.UtcNow;

        context.ChangeTracker.Clear();
        var softDeletedDate = (await context.ReportSchedule.FindAsync(report.Id))!.SoftDeletedDate;
        Assert.NotNull(softDeletedDate);
        Assert.InRange(softDeletedDate.Value, before, after);
        Assert.Equal(TimeSpan.Zero, softDeletedDate.Value.Offset);
    }

    /// <summary>
    /// A finished report ages from CompletedDate, so its soft delete records no date.
    /// </summary>
    [Theory]
    [InlineData(ScheduleStatus.Submitted)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public async Task SoftDeleteByReportTrackingIdAsync_FinishedReport_LeavesSoftDeletedDateNull(ScheduleStatus status)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), status);
        await SeedAsync(context, report);

        await sut.SoftDeleteByReportTrackingIdAsync(report.Id);

        context.ChangeTracker.Clear();
        var updated = await context.ReportSchedule.FindAsync(report.Id);
        Assert.True(updated!.IsDeleted);
        Assert.Null(updated.SoftDeletedDate);
    }

    [Fact]
    public async Task RestoreByReportTrackingIdAsync_UnfinishedReport_ClearsSoftDeletedDate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), ScheduleStatus.Scheduled, isDeleted: true,
            softDeletedDate: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedAsync(context, report);

        await sut.RestoreByReportTrackingIdAsync(report.Id);

        context.ChangeTracker.Clear();
        Assert.Null((await context.ReportSchedule.FindAsync(report.Id))!.SoftDeletedDate);
    }

    [Fact]
    public async Task SoftDeleteByReportTrackingIdAsync_AfterRestore_RecordsNewSoftDeletedDate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var report = CreateReport(Guid.NewGuid().ToString(), ScheduleStatus.Scheduled);
        await SeedAsync(context, report);

        await sut.SoftDeleteByReportTrackingIdAsync(report.Id);
        context.ChangeTracker.Clear();
        var firstDelete = (await context.ReportSchedule.FindAsync(report.Id))!.SoftDeletedDate;

        await sut.RestoreByReportTrackingIdAsync(report.Id);
        await Task.Delay(20);
        await sut.SoftDeleteByReportTrackingIdAsync(report.Id);

        context.ChangeTracker.Clear();
        var secondDelete = (await context.ReportSchedule.FindAsync(report.Id))!.SoftDeletedDate;
        Assert.NotNull(firstDelete);
        Assert.NotNull(secondDelete);
        Assert.True(secondDelete > firstDelete);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WhenDeleted_StampsOnlyUnfinishedReports()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var scheduled = CreateReport(facilityId, ScheduleStatus.Scheduled);
        var submitted = CreateReport(facilityId, ScheduleStatus.Submitted);
        var bypassed = CreateReport(facilityId, ScheduleStatus.CompletedNotSubmitted);
        await SeedAsync(context, scheduled, submitted, bypassed);

        var before = DateTimeOffset.UtcNow;
        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);
        var after = DateTimeOffset.UtcNow;

        context.ChangeTracker.Clear();
        var scheduledDate = (await context.ReportSchedule.FindAsync(scheduled.Id))!.SoftDeletedDate;
        Assert.NotNull(scheduledDate);
        Assert.InRange(scheduledDate.Value, before, after);
        Assert.Null((await context.ReportSchedule.FindAsync(submitted.Id))!.SoftDeletedDate);
        Assert.Null((await context.ReportSchedule.FindAsync(bypassed.Id))!.SoftDeletedDate);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WhenRestored_ClearsSoftDeletedDate()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var scheduled = CreateReport(facilityId, ScheduleStatus.Scheduled, isDeleted: true, softDeletedDate: yesterday);
        // Deleted while in flight and finished afterwards: restore still clears the stale date.
        var submitted = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: true, softDeletedDate: yesterday);
        await SeedAsync(context, scheduled, submitted);

        await sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        context.ChangeTracker.Clear();
        Assert.Null((await context.ReportSchedule.FindAsync(scheduled.Id))!.SoftDeletedDate);
        Assert.Null((await context.ReportSchedule.FindAsync(submitted.Id))!.SoftDeletedDate);
    }

    private static ReportScheduleModel ToModel(ReportSchedule report, ScheduleStatus status)
    {
        return new ReportScheduleModel
        {
            Id = report.Id,
            FacilityId = report.FacilityId,
            ReportStartDate = report.ReportStartDate,
            ReportEndDate = report.ReportEndDate,
            Frequency = report.Frequency,
            EnableSubmission = report.EnableSubmission,
            Status = status,
            IsDeleted = report.IsDeleted
        };
    }

    #endregion

    private static ReportSchedule CreateReport(string facilityId,
                                               ScheduleStatus status,
                                               bool isDeleted = false,
                                               DateTimeOffset? softDeletedDate = null)
    {
        return new ReportSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Status = status,
            IsDeleted = isDeleted,
            SoftDeletedDate = softDeletedDate,
            CreateDate = DateTime.UtcNow,
            ModifyDate = null
        };
    }

    private static ReportPopulation CreateReportPopulation(string facilityId, string reportType, int initialPopulationCount)
    {
        var reportPopulation = new ReportPopulation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ReportType = reportType,
            Measure = reportType,
            CreateDate = DateTime.UtcNow
        };

        var initialPopulation = new GroupPopulation
        {
            PopulationId = "initial-population",
            PopulationCodeJson = "{}",
            TotalPopulationCount = initialPopulationCount
        };

        for (var populationIndex = 0; populationIndex < initialPopulationCount; populationIndex++)
        {
            initialPopulation.MeasureReportPopulations.Add(new MeasureReportPopulation
            {
                MeasureReportId = $"{reportType}-measure-report-{populationIndex}",
                PopulationCount = 1
            });
        }

        reportPopulation.GroupPopulations.Add(initialPopulation);
        return reportPopulation;
    }

    private static async Task SeedAsync(ReportDbContext context, params ReportSchedule[] reports)
    {
        await context.ReportSchedule.AddRangeAsync(reports);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    #endregion
}