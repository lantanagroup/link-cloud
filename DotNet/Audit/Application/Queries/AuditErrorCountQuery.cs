using LantanaGroup.Link.Audit.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Audit;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Audit.Application.Queries;

public static class AuditErrorCountQuery
{
    public static async Task<AuditErrorCount> ExecuteAsync(
        IQueryable<AuditLog> logs,
        int hours,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var end = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        var start = end.AddHours(-hours);
        var errors = await logs.CountAsync(
            log => log.CreatedOn >= start
                   && log.CreatedOn < end
                   && log.Notes != null
                   && EF.Functions.Like(log.Notes, "%fail%"),
            cancellationToken);

        return new AuditErrorCount
        {
            Hours = hours,
            Errors = errors,
            WindowStartUtc = start,
            WindowEndUtc = end
        };
    }
}
