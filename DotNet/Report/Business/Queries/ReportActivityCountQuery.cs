using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Business.Queries;

/// <summary>
/// Database-side schedule aggregates. Callers pass an already-filtered query.
/// </summary>
public static class ReportActivityCountQuery
{
    private static readonly ScheduleStatus[] InFlight =
    [
        ScheduleStatus.New,
        ScheduleStatus.Scheduled,
        ScheduleStatus.EndOfPeriod
    ];

    public static async Task<ReportActivityCounts> ExecuteAsync(
        IQueryable<ReportSchedule> schedules,
        int days,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(utcNow);
        var windowStart = today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEnd = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var statusRows = await schedules
            .GroupBy(schedule => schedule.Status)
            .Select(group => new StatusRow(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        long inFlight = 0;
        long submitted = 0;
        long notSubmitted = 0;
        long failed = 0;
        foreach (var row in statusRows)
        {
            switch (row.Status)
            {
                case ScheduleStatus.New:
                case ScheduleStatus.Scheduled:
                case ScheduleStatus.EndOfPeriod:
                    inFlight += row.Count;
                    break;
                case ScheduleStatus.Submitted:
                    submitted += row.Count;
                    break;
                case ScheduleStatus.CompletedNotSubmitted:
                    notSubmitted += row.Count;
                    break;
                default:
                    failed += row.Count;
                    break;
            }
        }

        var oldest = await schedules
            .Where(schedule => InFlight.Contains(schedule.Status))
            .MinAsync(schedule => (DateTime?)schedule.CreateDate, cancellationToken);

        var dayRows = await schedules
            .Where(schedule => schedule.CreateDate >= windowStart && schedule.CreateDate < windowEnd)
            .GroupBy(schedule => new { schedule.CreateDate.Year, schedule.CreateDate.Month, schedule.CreateDate.Day })
            .Select(group => new DayRow(group.Key.Year, group.Key.Month, group.Key.Day, group.Count()))
            .ToListAsync(cancellationToken);

        var byDay = dayRows.ToDictionary(
            row => new DateOnly(row.Year, row.Month, row.Day),
            row => row.Count);

        var created = new List<ReportDayCount>(days);
        for (var offset = 0; offset < days; offset++)
        {
            var day = today.AddDays(offset - (days - 1));
            created.Add(new ReportDayCount
            {
                Day = day.ToString("yyyy-MM-dd"),
                Count = byDay.GetValueOrDefault(day)
            });
        }

        return new ReportActivityCounts
        {
            InFlight = inFlight,
            Submitted = submitted,
            NotSubmitted = notSubmitted,
            Failed = failed,
            OldestInFlightUtc = oldest is DateTime value
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : null,
            CreatedPerDay = created
        };
    }

    public static IQueryable<ReportSchedule> Apply(
        IQueryable<ReportSchedule> schedules,
        IReadOnlyCollection<string>? includeFacilityIds,
        IReadOnlyCollection<string>? excludeFacilityIds)
    {
        var query = schedules.Where(schedule => !schedule.IsDeleted.HasValue || schedule.IsDeleted == false);
        if (includeFacilityIds is not null)
            query = query.Where(schedule => includeFacilityIds.Contains(schedule.FacilityId));
        if (excludeFacilityIds is not null)
            query = query.Where(schedule => !excludeFacilityIds.Contains(schedule.FacilityId));
        return query;
    }

    private sealed record StatusRow(ScheduleStatus Status, int Count);
    private sealed record DayRow(int Year, int Month, int Day, int Count);
}
