using System.Linq.Expressions;
using LantanaGroup.Link.Report.Controllers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// The <c>active=true</c> branch of <c>GET /facilities/{facilityId}</c>, which asks which
/// report schedules are still in flight.
///
/// It filters on <c>ScheduleStatusExtensions.TerminalStatuses</c>, so a report that finished
/// without submitting has to be excluded just as a submitted one is. Before that set existed
/// the branch compared against <c>Submitted</c> alone, and a bypassed report would have been
/// reported as active forever.
///
/// Asserts by capturing the predicate handed to the manager and evaluating it, rather than
/// by checking which rows come back — the filtering is the predicate, and a stubbed manager
/// returning a canned list would prove nothing about it.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportScheduleControllerActiveFilterTests
{
    private const string FacilityId = "facility-a";

    [Theory]
    [InlineData(ScheduleStatus.Submitted)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public async Task GetByFacilityId_Active_ExcludesTerminalSchedules(ScheduleStatus terminalStatus)
    {
        var predicate = await CaptureActivePredicateAsync();

        Assert.False(predicate(Schedule(terminalStatus)),
            $"{terminalStatus} is terminal and must not be reported as active.");
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.Scheduled)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    public async Task GetByFacilityId_Active_KeepsInFlightSchedules(ScheduleStatus inFlightStatus)
    {
        var predicate = await CaptureActivePredicateAsync();

        Assert.True(predicate(Schedule(inFlightStatus)),
            $"{inFlightStatus} is still in flight and must be reported as active.");
    }

    /// <summary>
    /// Guards the filter against being satisfied by something other than terminality — a
    /// predicate that excluded everything would pass the two theories above on its own.
    /// </summary>
    [Fact]
    public async Task GetByFacilityId_Active_SeparatesTerminalFromInFlight()
    {
        var predicate = await CaptureActivePredicateAsync();

        var kept = new[]
        {
            ScheduleStatus.New, ScheduleStatus.Scheduled, ScheduleStatus.EndOfPeriod,
            ScheduleStatus.Submitted, ScheduleStatus.CompletedNotSubmitted
        }.Where(s => predicate(Schedule(s))).ToArray();

        Assert.Equal(
            [ScheduleStatus.New, ScheduleStatus.Scheduled, ScheduleStatus.EndOfPeriod],
            kept);
    }

    [Fact]
    public async Task GetByFacilityId_Active_StillScopesToTheFacility()
    {
        var predicate = await CaptureActivePredicateAsync();

        Assert.False(predicate(Schedule(ScheduleStatus.New, facilityId: "another-facility")));
    }

    /// <summary>
    /// Soft-deleted rows stay hidden unless asked for, independently of status.
    /// </summary>
    [Fact]
    public async Task GetByFacilityId_Active_ExcludesDeletedByDefault()
    {
        var predicate = await CaptureActivePredicateAsync();

        Assert.False(predicate(Schedule(ScheduleStatus.New, isDeleted: true)));
        Assert.True(predicate(Schedule(ScheduleStatus.New, isDeleted: null)));
    }

    [Fact]
    public async Task GetByFacilityId_Active_IncludesDeletedWhenRequested()
    {
        var predicate = await CaptureActivePredicateAsync(includeDeleted: true);

        Assert.True(predicate(Schedule(ScheduleStatus.New, isDeleted: true)));
        Assert.False(predicate(Schedule(ScheduleStatus.Submitted, isDeleted: true)));
    }

    /// <summary>
    /// Invokes the action and returns the compiled predicate it handed to the manager.
    /// </summary>
    private static async Task<Func<ReportSchedule, bool>> CaptureActivePredicateAsync(bool includeDeleted = false)
    {
        Expression<Func<ReportSchedule, bool>>? captured = null;

        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportSchedule, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<ReportSchedule, bool>>, CancellationToken>((expr, _) => captured = expr)
            .ReturnsAsync([]);

        var controller = new ReportScheduleController(
            Mock.Of<ILogger<ReportScheduleController>>(),
            Mock.Of<IDatabase>(),
            manager.Object);

        await controller.GetByFacilityId(FacilityId, active: true, blocking: false, includeDeleted: includeDeleted);

        Assert.NotNull(captured);
        return captured!.Compile();
    }

    private static ReportSchedule Schedule(
        ScheduleStatus status,
        string facilityId = FacilityId,
        bool? isDeleted = false) => new()
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Status = status,
            IsDeleted = isDeleted,
            CreateDate = DateTime.UtcNow
        };
}
