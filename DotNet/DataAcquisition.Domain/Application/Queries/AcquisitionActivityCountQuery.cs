using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.EntityFrameworkCore;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public static class AcquisitionActivityCountQuery
{
    public static async Task<AcquisitionActivityCounts> ExecuteAsync(
        IQueryable<DataAcquisitionLog> logs,
        int days,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var active = logs.Where(log => !log.IsDeleted);
        var failedTotal = await active.CountAsync(
            log => log.Status == RequestStatus.Failed || log.Status == RequestStatus.MaxRetriesReached,
            cancellationToken);

        var today = DateOnly.FromDateTime(utcNow);
        var windowStart = today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEnd = today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var dayRows = await active
            .Where(log => log.ExecutionDate >= windowStart && log.ExecutionDate < windowEnd)
            .GroupBy(log => new
            {
                log.ExecutionDate!.Value.Year,
                log.ExecutionDate!.Value.Month,
                log.ExecutionDate!.Value.Day
            })
            .Select(group => new DayRow(
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                group.Count(),
                group.Count(log => log.Status == RequestStatus.Failed || log.Status == RequestStatus.MaxRetriesReached)))
            .ToListAsync(cancellationToken);

        var byDay = dayRows.ToDictionary(
            row => new DateOnly(row.Year, row.Month, row.Day),
            row => row);

        var series = new List<AcquisitionDayCount>(days);
        for (var offset = 0; offset < days; offset++)
        {
            var day = today.AddDays(offset - (days - 1));
            byDay.TryGetValue(day, out var row);
            series.Add(new AcquisitionDayCount
            {
                Day = day.ToString("yyyy-MM-dd"),
                Total = row?.Total ?? 0,
                Failed = row?.Failed ?? 0
            });
        }

        return new AcquisitionActivityCounts
        {
            FailedTotal = failedTotal,
            Days = series
        };
    }

    private sealed record DayRow(int Year, int Month, int Day, int Total, int Failed);
}
