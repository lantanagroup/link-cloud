using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IScheduledReportManager
{
    /// <summary>
    /// Ensures a <see cref="ScheduledReportEntity"/> row exists for the given
    /// <paramref name="report"/>. If a row with a matching <c>ReportTrackingId</c>
    /// already exists it is returned as-is; otherwise a new row is inserted.
    ///
    /// This method is safe to call concurrently from multiple processes — it catches
    /// the unique-constraint violation that occurs when two workers race to insert
    /// the same <c>ReportTrackingId</c> and falls back to a read.
    /// </summary>
    Task<ScheduledReportEntity> EnsureCreatedAsync(ScheduledReport report, CancellationToken cancellationToken = default);
}

public class ScheduledReportManager : IScheduledReportManager
{
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly ILogger<ScheduledReportManager> _logger;

    public ScheduledReportManager(DataAcquisitionDbContext dbContext, ILogger<ScheduledReportManager> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScheduledReportEntity> EnsureCreatedAsync(ScheduledReport report, CancellationToken cancellationToken = default)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        if (string.IsNullOrWhiteSpace(report.ReportTrackingId))
            throw new ArgumentException("ReportTrackingId must not be null or empty.", nameof(report));

        if (!Guid.TryParse(report.ReportTrackingId, out var reportTrackingId))
            throw new ArgumentException("ReportTrackingId must be a valid GUID.", nameof(report));

        // Fast path — row already exists (PK lookup).
        var existing = await _dbContext.ScheduledReports
            .FindAsync(new object[] { reportTrackingId }, cancellationToken);

        if (existing != null)
            return existing;

        // Slow path — insert, handling the race where another process inserts first.
        var entity = new ScheduledReportEntity
        {
            ReportTrackingId = reportTrackingId,
            Frequency = report.Frequency,
            StartDate = report.StartDate,
            EndDate = report.EndDate,
            ReportTypes = report.ReportTypes != null
                ? string.Join(",", report.ReportTypes)
                : null,
            CreateDate = DateTime.UtcNow,
        };

        _dbContext.ScheduledReports.Add(entity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogDebug(
                ex,
                "Concurrent insert for ReportTrackingId={ReportTrackingId} — falling back to read.",
                report.ReportTrackingId);

            // Detach the failed entity so the DbContext is clean.
            _dbContext.Entry(entity).State = EntityState.Detached;

            // The other process won the race — read their row.
            return await _dbContext.ScheduledReports
                .FindAsync(new object[] { reportTrackingId }, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"ScheduledReport with ReportTrackingId={reportTrackingId} not found after concurrent insert.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // SQL Server error 2601 = unique index violation, 2627 = unique constraint violation.
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("2601") || message.Contains("2627")
            || message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
