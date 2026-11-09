using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Domain.Managers;

/// <summary>
/// Finds the reports eligible for retention cleanup. See dev-docs/retention-check.md.
/// </summary>
public interface IRetentionEligibilityQuery
{
    /// <summary>
    /// The eligible reports whose governing date is strictly before <paramref name="cutoff"/>, oldest first, at
    /// most <paramref name="maxReports"/> of them.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxReports"/> is less than 1.
    /// </exception>
    Task<IReadOnlyList<RetentionEligibleReport>> FindAsync(DateTimeOffset cutoff,
                                                           int maxReports,
                                                           CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds the reports eligible for retention cleanup: retention status <c>None</c>, and a governing date
/// strictly before the cutoff, the eligibility rule in ADR 08, Workflow step 1.
/// </summary>
/// <remarks>
/// <para>
/// The governing date is <c>CompletedDate</c> when the report finished, even if it was soft-deleted later, and
/// otherwise <c>SoftDeletedDate</c>, which is only ever set on a report that never finished. A report with
/// neither has no clock and is never selected.
/// </para>
/// <para>
/// Soft-deleted reports are deliberately included, so this query must not filter on <c>IsDeleted</c> the way
/// the other report queries do.
/// </para>
/// <para>
/// This only reads. A report found here can still be taken by another run before this one records it;
/// <see cref="IRetentionCleanupManager.CreateAsync"/> claims reports atomically and skips those.
/// </para>
/// </remarks>
public class RetentionEligibilityQuery : IRetentionEligibilityQuery
{
    private readonly ReportDbContext _context;

    /// <summary>
    /// Creates the query.
    /// </summary>
    public RetentionEligibilityQuery(ReportDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetentionEligibleReport>> FindAsync(DateTimeOffset cutoff,
                                                                        int maxReports,
                                                                        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxReports, 1);

        var cutoffUtc = cutoff.ToUniversalTime();

        return await _context.ReportSchedule
            .AsNoTracking()
            .Where(r => r.RetentionStatus == ReportRetentionStatus.None)
            .Where(r => (r.CompletedDate ?? r.SoftDeletedDate) < cutoffUtc)
            .OrderBy(r => r.CompletedDate ?? r.SoftDeletedDate)
            .ThenBy(r => r.Id)
            .Take(maxReports)
            .Select(r => new RetentionEligibleReport(r.Id, r.FacilityId))
            .ToListAsync(cancellationToken);
    }
}
