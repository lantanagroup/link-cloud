using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface IDataAcquisitionLogQueries
{
    /// <summary>
    /// Retrieves a list of TailingMessageModel objects that represent the tailing messages for data acquisition logs.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default);

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
    private readonly ILogger<DataAcquisitionLogQueries> _logger;

    public DataAcquisitionLogQueries(IDatabase database, DataAcquisitionDbContext dbContext, ILogger<DataAcquisitionLogQueries> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    public async Task<IEnumerable<TailingMessageModel>> GetTailingMessages(CancellationToken cancellationToken = default)
    {
        var completedOrFailedStatuses = new[] { RequestStatus.Completed };

        try
        {
            // Group and aggregate in SQL
            var query = _dbContext.DataAcquisitionLogs
                .Where(log =>
                    log.ReportTrackingId != null &&
                    log.CorrelationId != null &&
                    log.ReportStartDate != null &&
                    log.ReportEndDate != null)
                .GroupBy(log => new
                {
                    log.FacilityId,
                    log.ReportTrackingId,
                    log.CorrelationId,
                    log.ReportStartDate,
                    log.ReportEndDate,
                    log.QueryPhase,
                })
                .Where(g => g.All(log => log.Status != null && completedOrFailedStatuses.Contains(log.Status.Value) && !log.TailSent))
                .Select(g => new TailingMessageModel
                {
                    Key = g.Key.FacilityId ?? string.Empty,
                    CorrelationId = g.Key.CorrelationId ?? string.Empty,
                    LogIds = g.Select(x => x.Id).ToList(),
                    ResourceAcquired = new ResourceAcquired
                    {
                        PatientId = g.Select(x => x.PatientId).FirstOrDefault() ?? string.Empty,
                        QueryType = g.Select(x => x.QueryPhase.ToString()).FirstOrDefault() ?? string.Empty,
                        ReportableEvent = g.Select(x => x.ReportableEvent).FirstOrDefault() ?? default,
                        AcquisitionComplete = true,
                        ScheduledReports = new List<ScheduledReport>
                        {
                             g.FirstOrDefault().ScheduledReport
                        }
                    }
                });

            return await query.ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Log cancellation if needed
            _logger.LogWarning("GetTailingMessages operation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            // Log the error (replace with your logger if available)
            _logger.LogError(ex, "An error occurred while retrieving tailing messages.");
            throw new InvalidOperationException("An error occurred while retrieving tailing messages.", ex);
        }
    }
}
