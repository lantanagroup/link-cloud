using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportEntryManagerAreAllEntriesCompleteTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportEntryManagerAreAllEntriesCompleteTests(ReportIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task AreAllEntriesCompleteAsync_AllTerminal_ReturnsTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.Submitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p2",
            ReportingStatus.FailedValidation, SubmissionStatus.Submitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p3",
            ReportingStatus.NotReportable, SubmissionStatus.NotEligable);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.True(result);
    }

    /// <summary>
    /// A bypassed report's patients land on NotSubmitted rather than Submitted. If that
    /// status is not treated as terminal, AreAllEntriesCompleteAsync never returns true,
    /// ReportManifestProducer.Produce short-circuits on every call, and the manifest is
    /// never written to internal/ at all -- a bypassed report that hangs silently rather
    /// than failing. This is the regression most likely to be missed, because a hung
    /// report looks like a slow one.
    /// </summary>
    [Fact]
    public async Task AreAllEntriesCompleteAsync_BypassedSubmission_ReturnsTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId, enableSubmission: false);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.NotSubmitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p2",
            ReportingStatus.FailedValidation, SubmissionStatus.NotSubmitted);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.True(result);
    }

    /// <summary>
    /// Bypass is decided per report, but a regenerated schedule can inherit patients from
    /// a run that did submit, so the two statuses have to coexist within one schedule.
    /// </summary>
    [Fact]
    public async Task AreAllEntriesCompleteAsync_MixedTerminalStatuses_ReturnsTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.Submitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p2",
            ReportingStatus.PassedValidation, SubmissionStatus.NotSubmitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p3",
            ReportingStatus.NotReportable, SubmissionStatus.NotEligable);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.True(result);
    }

    /// <summary>
    /// Submitting is the state ValidationCompleteListener assigns before producing, and it
    /// is the state a bypassed entry would be stranded in if the gate skipped the produce
    /// without assigning a terminal status.
    /// </summary>
    [Fact]
    public async Task AreAllEntriesCompleteAsync_StrandedInSubmitting_ReturnsFalse()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId, enableSubmission: false);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.NotSubmitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p2",
            ReportingStatus.PassedValidation, SubmissionStatus.Submitting);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAllEntriesCompleteAsync_OneIncomplete_ReturnsFalse()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.Submitted);
        await SeedEntryAsync(context, facilityId, scheduleId, "p2",
            ReportingStatus.PatientIdentified, null);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAllEntriesCompleteAsync_TerminalReportingButNullSubmission_ReturnsFalse()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        await SeedEntryAsync(context, facilityId, scheduleId, "p1",
            ReportingStatus.PassedValidation, null);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAllEntriesCompleteAsync_NoEntries_ReturnsTrue()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var scheduleId = Guid.NewGuid();
        var facilityId = Guid.NewGuid().ToString();
        await SeedReportScheduleAsync(context, scheduleId, facilityId);

        var result = await sut.AreAllEntriesCompleteAsync(facilityId, scheduleId);

        Assert.True(result);
    }

    [Fact]
    public async Task AreAllEntriesCompleteAsync_DoesNotCrossContaminateSchedules()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var sut = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = Guid.NewGuid().ToString();
        var scheduleA = Guid.NewGuid();
        var scheduleB = Guid.NewGuid();
        await SeedReportScheduleAsync(context, scheduleA, facilityId);
        await SeedReportScheduleAsync(context, scheduleB, facilityId);

        // Schedule A is complete
        await SeedEntryAsync(context, facilityId, scheduleA, "p1",
            ReportingStatus.PassedValidation, SubmissionStatus.Submitted);

        // Schedule B is NOT complete
        await SeedEntryAsync(context, facilityId, scheduleB, "p2",
            ReportingStatus.PendingValidation, null);

        Assert.True(await sut.AreAllEntriesCompleteAsync(facilityId, scheduleA));
        Assert.False(await sut.AreAllEntriesCompleteAsync(facilityId, scheduleB));
    }

    #region Helpers

    private static async Task SeedReportScheduleAsync(
        ReportDbContext context,
        Guid scheduleId,
        string facilityId,
        bool enableSubmission = true)
    {
        if (await context.Set<ReportSchedule>().AnyAsync(rs => rs.Id == scheduleId))
            return;

        context.Set<ReportSchedule>().Add(new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = facilityId,
            CreateDate = DateTime.UtcNow,
            ReportStartDate = DateTime.UtcNow.AddDays(-30),
            ReportEndDate = DateTime.UtcNow.AddDays(30),
            EnableSubmission = enableSubmission,
            EndOfReportPeriodJobHasRun = false,
            Frequency = 0,
            Status = 0,
            IsDeleted = false
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedEntryAsync(
        ReportDbContext context,
        string facilityId,
        Guid reportScheduleId,
        string patientId,
        ReportingStatus reportingStatus,
        SubmissionStatus? submissionStatus)
    {
        context.ReportEntry.Add(new ReportEntry
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ReportScheduleId = reportScheduleId,
            PatientId = patientId,
            ReportingStatus = reportingStatus,
            SubmissionStatus = submissionStatus,
            CreateDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    #endregion
}
