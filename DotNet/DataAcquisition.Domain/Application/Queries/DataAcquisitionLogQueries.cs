using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogQueries
{
    /// <summary>
    /// Retrieves a list of TailingMessageModel objects that represent the tailing messages for data acquisition logs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a complete data acquisition log by its ID, including related entities such as ScheduledReport, ReportableEvent, and FhirQuery.
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    Task<DataAcquisitionLog> GetCompleteLogAsync(string logId, CancellationToken cancellationToken = default);
}

public class DataAcquisitionLogQueries : IDataAcquisitionLogQueries
{
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves a complete data acquisition log by its ID, including related entities such as ScheduledReport, ReportableEvent, and FhirQuery.
    /// </summary>
    /// <param name="logId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    public async Task<DataAcquisitionLog> GetCompleteLogAsync(string logId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logId))
        {
            throw new ArgumentNullException(nameof(logId), "Log ID cannot be null or empty.");
        }

        var log = await _dbContext.DataAcquisitionLogs
            .Include(l => l.FhirQuery)
            .FirstOrDefaultAsync(l => l.Id == logId, cancellationToken);

        if (log == null)
        {
            throw new KeyNotFoundException($"Data acquisition log with ID '{logId}' not found.");
        }

        return log;
    }

    /// <summary>
    /// Here is the T-SQL equivalent of the LINQ query:
    /// select distinct l.patientId, l.ReportTrackingId, l.CorrelationId 
    /// from DataAcquisitionLog l
    /// where l.FacilityId = :1
    /// and l.Status not in ('Pending', 'Processing')
    /// and not exists(select 1 from DataAcquisitionLog l1 where l.ReportTrackingId = l1.ReportTrackingId and l.CorrelationId = l1.ReportTrackingId and l.Status in ('Pending', 'Processing'))
    /// and NOT ISNULL(l.patientId, '') = ''
    /// group by l.patientId, l.facilityId, l.ReportTrackingId, l.CorrelationId
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default)
    {
        var pendingStatuses = new[] { RequestStatus.Pending, RequestStatus.Processing };

        // Materialize the relevant log data first to avoid translation issues
        var logs = await _dbContext.DataAcquisitionLogs
            .Where(log =>
                log.Status != null &&
                !pendingStatuses.Contains(log.Status.Value) &&
                !((log.PatientId ?? "").Trim() == "") &&
                !log.TailSent)
            .Select(log => new
            {
                log.PatientId,
                log.FacilityId,
                log.CorrelationId,
                log.QueryPhase,
                log.ScheduledReport,
                log.ReportableEvent,
                ReportTrackingId = log.ScheduledReport != null ? log.ScheduledReport.ReportTrackingId : null
            })
            .ToListAsync(cancellationToken);

        var logsWithNoPending = logs
            .Where(log =>
                !_dbContext.DataAcquisitionLogs.Any(l1 =>
                    l1.ScheduledReport != null &&
                    l1.ScheduledReport.ReportTrackingId == log.ReportTrackingId &&
                    l1.CorrelationId == log.CorrelationId &&
                    l1.Status != null &&
                    pendingStatuses.Contains(l1.Status.Value) &&
                    l1.TailSent))
            .ToList();

        var grouped = logsWithNoPending
            .GroupBy(log => new
            {
                log.PatientId,
                log.FacilityId,
                log.CorrelationId,
                log.QueryPhase,
                log.ScheduledReport,
                log.ReportableEvent
            })
            .Select(g => new TailingMessageModel
            {
                Key = g.Key.FacilityId ?? string.Empty,
                CorrelationId = g.Key.CorrelationId ?? string.Empty,
                ResourceAcquired = new ResourceAcquired
                {
                    PatientId = g.Key.PatientId ?? string.Empty,
                    QueryType = g.Key.QueryPhase?.ToString() ?? string.Empty,
                    ReportableEvent = g.Key.ReportableEvent ?? default,
                    AcquisitionComplete = true,
                    ScheduledReports = g.Key.ScheduledReport != null
                        ? new List<ScheduledReport> { g.Key.ScheduledReport }
                        : new List<ScheduledReport>()
                }
            })
            .ToList();

        return grouped;
    }
}
