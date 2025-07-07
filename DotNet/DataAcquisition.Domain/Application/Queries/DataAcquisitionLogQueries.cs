using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using IDatabase = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.IDatabase;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;

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

    /// <summary>
    /// Retrieves a data acquisition log entry based on the specified facility ID, report tracking ID, and resource
    /// type.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility associated with the log entry. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking entry associated with the log. Cannot be null or empty.</param>
    /// <param name="resourceType">The type of resource associated with the log entry. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="DataAcquisitionLog"/>
    /// object  matching the specified criteria, or <see langword="null"/> if no matching log entry is found.</returns>
    Task<DataAcquisitionLog> GetLogByFacilityIdAndReportTrackingIdAndResourceType(string facilityId, string reportTrackingId, string resourceType, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the count of non-reference logs that are incomplete for a specified facility, report,
    /// and correlation.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking. Cannot be null or empty.</param>
    /// <param name="correlationId">The unique identifier used to correlate related logs. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of non-reference logs 
    /// that are incomplete for the specified parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facilityId"/>, <paramref name="reportTrackingId"/>, or <paramref
    /// name="correlationId"/> is null or empty.</exception>
    Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default);
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
            .ThenInclude(l => l.ResourceReferenceTypes)
            .FirstOrDefaultAsync(l => l.Id == logId, cancellationToken);

        if (log == null)
        {
            throw new KeyNotFoundException($"Data acquisition log with ID '{logId}' not found.");
        }

        return log;
    }

    /// <summary>
    /// Asynchronously retrieves the count of non-reference logs that are incomplete for a specified facility, report,
    /// and correlation.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking. Cannot be null or empty.</param>
    /// <param name="correlationId">The unique identifier used to correlate related logs. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the count of non-reference logs 
    /// that are incomplete for the specified parameters.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facilityId"/>, <paramref name="reportTrackingId"/>, or <paramref
    /// name="correlationId"/> is null or empty.</exception>
    public async Task<int> GetCountOfNonRefLogsIncompleteAsync(string facilityId, string reportTrackingId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentNullException(nameof(correlationId), "Correlation ID cannot be null or empty.");

        return await _dbContext.DataAcquisitionLogs
            .CountAsync(log => log.FacilityId == facilityId &&
                               log.ReportTrackingId == reportTrackingId &&
                               log.CorrelationId == correlationId &&
                               (log.Status == null || log.Status != RequestStatus.Completed) &&
                               !log.TailSent &&
                               log.FhirQuery.Any(fq => fq.isReference == false) // Ensure we only count non-reference logs
                               , cancellationToken);
    }

    /// <summary>
    /// Retrieves a data acquisition log based on the specified facility ID, report tracking ID, and resource type.
    /// </summary>
    /// <param name="facilityId">The unique identifier of the facility. Cannot be null or empty.</param>
    /// <param name="reportTrackingId">The unique identifier of the report tracking. Cannot be null or empty.</param>
    /// <param name="resourceType">The type of resource associated with the log. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Optional.</param>
    /// <returns>A <see cref="DataAcquisitionLog"/> object that matches the specified criteria, or <see langword="null"/> if no
    /// matching log is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="facilityId"/>, <paramref name="reportTrackingId"/>, or <paramref name="resourceType"/>
    /// is null or empty.</exception>
    public async Task<DataAcquisitionLog> GetLogByFacilityIdAndReportTrackingIdAndResourceType(string facilityId, string reportTrackingId, string resourceType, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentNullException(nameof(facilityId), "Facility ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(reportTrackingId))
            throw new ArgumentNullException(nameof(reportTrackingId), "Report Tracking ID cannot be null or empty.");
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentNullException(nameof(resourceType), "Resource Type cannot be null or empty.");

        var resourceTypeEnum = Enum.Parse<Hl7.Fhir.Model.ResourceType>(resourceType, ignoreCase: true);

        // I don't believe this will be performant as it will load all logs for the facility and report tracking ID.
        // Consider the following optimizations for whoever looks at this in the future:
        // 1. Add an index on FacilityId and ReportTrackingId in the database.
        // 2. Consider either abstracting the ResourceTypes to a separate table or changing the structure of ResourceTypes to comma-separated string.
        // 3. In-line sql to query this information.
        var candidates = await _dbContext.DataAcquisitionLogs
            .Include(dl => dl.FhirQuery)
            .Where(dl => dl.FacilityId == facilityId &&
                 dl.ReportTrackingId == reportTrackingId &&
                 dl.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);

        var log = candidates
            .FirstOrDefault(dl =>
                dl.FhirQuery.SelectMany(fq => fq.ResourceTypes)
                           .Contains(resourceTypeEnum));

        return log;
    }

    /// <summary>
    /// Here is the T-SQL equivalent of the LINQ query:
    /// SELECT
    /// l.FacilityId,
    /// l.ReportTrackingId,
    /// l.CorrelationId,
    /// l.ReportStartDate,
    /// l.ReportEndDate,
    /// l.QueryPhase,
    /// -- Aggregate log IDs as a comma-separated string (SQL Server syntax)
    /// STRING_AGG(l.Id, ',') AS LogIds,
    /// -- Get the first PatientId, QueryPhase, ReportableEvent, ScheduledReport (if needed, use subqueries or window functions)
    /// MIN(l.PatientId) AS PatientId,
    /// MIN(l.QueryPhase) AS QueryType,
    /// MIN(l.ReportableEvent) AS ReportableEvent
    /// FROM
    /// DataAcquisitionLog l
    /// WHERE
    /// l.ReportTrackingId IS NOT NULL
    /// AND l.CorrelationId IS NOT NULL
    /// AND l.ReportStartDate IS NOT NULL
    /// AND l.ReportEndDate IS NOT NULL
    /// GROUP BY
    /// l.FacilityId,
    /// l.ReportTrackingId,
    /// l.CorrelationId,
    /// l.ReportStartDate,
    /// l.ReportEndDate,
    /// l.QueryPhase
    /// HAVING
    /// -- All logs in the group must have Status = 'Completed' and TailSent = 0(false)
    /// COUNT(*) = SUM(CASE WHEN l.Status = 'Completed' AND l.TailSent = 0 THEN 1 ELSE 0 END)
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
