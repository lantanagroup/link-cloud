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

    #endregion

    private static ReportSchedule CreateReport(string facilityId, ScheduleStatus status, bool isDeleted = false)
    {
        return new ReportSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Status = status,
            IsDeleted = isDeleted,
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