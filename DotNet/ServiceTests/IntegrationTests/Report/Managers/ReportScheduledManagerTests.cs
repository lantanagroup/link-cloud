using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Enums;
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

    #region Helper Methods

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

    private static async Task SeedAsync(ReportDbContext context, params ReportSchedule[] reports)
    {
        await context.ReportSchedule.AddRangeAsync(reports);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    #endregion
}