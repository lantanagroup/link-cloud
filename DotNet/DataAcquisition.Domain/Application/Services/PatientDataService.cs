using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using StringComparison = System.StringComparison;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IPatientDataService
{
    Task CreateLogEntries(GetPatientDataRequest request, CancellationToken cancellationToken);
    Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the log request for data acquisition.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="MissingFacilityConfigurationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    Task ExecuteLogRequest(AcquisitionRequest request, CancellationToken cancellationToken);
}

public class PatientDataService : IPatientDataService
{
    private readonly IDatabase _database;
    private readonly ILogger<PatientDataService> _logger;
    private readonly IFhirQueryConfigurationManager _fhirQueryManager;
    private readonly IQueryPlanManager _queryPlanManager;
    private readonly IQueryListProcessor _queryListProcessor;
    private readonly ProducerConfig _producerConfig;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly IFhirApiService _fhirApiService;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationManager fhirQueryManager,
        IQueryPlanManager queryPlanManager,
        IQueryListProcessor queryListProcessor,
        IReadFhirCommand readFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IFhirApiService fhirApiService,
        IDistributedSemaphoreProvider distributedSemaphoreProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));
        _producerConfig = new ProducerConfig { CompressionType = CompressionType.Zstd };
        _queryListProcessor = queryListProcessor ?? throw new ArgumentNullException(nameof(queryListProcessor));
        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _fhirApiService = fhirApiService ?? throw new ArgumentNullException(nameof(fhirApiService));
        _distributedSemaphoreProvider = distributedSemaphoreProvider;
    }

    public async Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var authenticationConfig = await _fhirQueryManager.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
            var queryConfig = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);

            var patient = await _readFhirCommand.ExecuteAsync(
                new ReadFhirCommandRequest(
                    request.FacilityId,
                    ResourceType.Patient,
                    TEMPORARYPatientIdPart(request.ConsumeResult.Value.PatientId),
                    queryConfig.FhirServerBaseUrl,
                    queryConfig),
                cancellationToken);

            var queryPlan = (await _queryPlanManager.FindAsync(
                q => q.FacilityId.ToLower() == request.FacilityId.ToLower(), cancellationToken))
                .FirstOrDefault();

            if (queryPlan == null)
                throw new MissingFacilityConfigurationException("Query Plan not found.");

            var resources = new List<Resource>();

            var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
            var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

            var referenceTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList();
            referenceTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList());

            resources.AddRange(await _queryListProcessor.ExecuteFacilityValidationRequest(
                initialQueries,
                request,
                queryConfig,
                request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
                queryPlan,
                referenceTypes,
                QueryPlanType.Initial.ToString()));

            resources.AddRange(await _queryListProcessor.ExecuteFacilityValidationRequest(
                supplementalQueries,
                request,
                queryConfig,
                request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
                queryPlan,
                referenceTypes,
                QueryPlanType.Supplemental.ToString()));

            return resources;
        }
        catch (DeadLetterException) { throw; }
        catch (TransientException) { throw; }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "Missing configuration for facility {FacilityId}", request.FacilityId.Sanitize());
            throw new DeadLetterException($"Missing configuration for facility {request.FacilityId}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating facility connection for {FacilityId}", request.FacilityId.Sanitize());
            throw new DeadLetterException($"Unexpected error validating facility connection for {request.FacilityId}", ex);
        }
    }

    public async Task CreateLogEntries(GetPatientDataRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var dataAcqRequested = request.ConsumeResult.Message.Value;

        if (dataAcqRequested == null || string.IsNullOrWhiteSpace(dataAcqRequested.PatientId) || string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ArgumentException("Invalid request data. PatientId and FacilityId must be provided.");
        }

        try
        {
            var fhirQueryConfiguration = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);
            var reportableEventTranslation = ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent(request.ConsumeResult.Value.ReportableEvent);
            var queryPlan = (await _queryPlanManager.FindAsync(
                q => q.FacilityId == request.FacilityId && q.Type == reportableEventTranslation, cancellationToken))
                ?.FirstOrDefault();

            if (fhirQueryConfiguration == null || queryPlan == null)
            {
                throw new MissingFacilityConfigurationException($"No configuration for {request.FacilityId} exists.");
            }

            var patientId = TEMPORARYPatientIdPart(dataAcqRequested.PatientId);

            var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
            var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

            var referenceStrTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList();
            referenceStrTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList());

            var referenceTypes = referenceStrTypes.Select(x =>
                new ResourceReferenceType
                {
                    FacilityId = request.FacilityId,
                    QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                    ResourceType = x,
                }).ToList();

            foreach (var schedReport in request.ConsumeResult.Message.Value.ScheduledReports)
            {
                if (request.QueryPlanType == QueryPlanType.Initial)
                {
                    try
                    {
                        await _dataAcquisitionLogManager.CreateAsync(
                            new DataAcquisitionLog
                            {
                                FacilityId = request.FacilityId,
                                CorrelationId = request.CorrelationId,
                                PatientId = request.ConsumeResult.Message.Value.PatientId,
                                ReportTrackingId = schedReport.ReportTrackingId,
                                ExecutionDate = System.DateTime.UtcNow,
                                Priority = AcquisitionPriority.Normal,
                                ReportableEvent = request.ConsumeResult.Message.Value.ReportableEvent,
                                Status = RequestStatus.Pending,
                                FhirVersion = "R4",
                                ReportEndDate = schedReport.EndDate,
                                ReportStartDate = schedReport.StartDate,
                                QueryType = FhirQueryType.Read,
                                QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                                ScheduledReport = schedReport,
                                TimeZone = fhirQueryConfiguration.TimeZone ?? "UTC",
                                TraceId = Activity.Current?.ParentId,
                                FhirQuery = new List<FhirQuery>
                                {
                                    new FhirQuery
                                    {
                                        QueryType = FhirQueryType.Read,
                                        ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                                        QueryParameters = new List<string>(),
                                        FacilityId = request.FacilityId,
                                        ResourceReferenceTypes = referenceTypes.Select(x =>
                                            new ResourceReferenceType
                                            {
                                                FacilityId = request.FacilityId,
                                                QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                                                ResourceType = x.ResourceType,
                                            }).ToList(),
                                    }
                                },
                            }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating log entry for facility {FacilityId} and patient {PatientId}", request.FacilityId.Sanitize(), dataAcqRequested.PatientId);
                        throw new DeadLetterException($"Error creating log entry for facility {request.FacilityId}", ex);
                    }
                }

                await _queryListProcessor.Process(
                        dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase) ? initialQueries : supplementalQueries,
                        request,
                        fhirQueryConfiguration,
                        queryPlan,
                        referenceTypes,
                        dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase) ? QueryPlanType.Initial.ToString() : QueryPlanType.Supplemental.ToString(),
                        schedReport,
                        cancellationToken);
            }
        }
        catch (DeadLetterException) { throw; }
        catch (TransientException) { throw; }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "Missing configuration for facility {FacilityId}", request.FacilityId.Sanitize());
            throw new DeadLetterException($"Missing configuration for facility {request.FacilityId}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating log entries for facility {FacilityId}", request.FacilityId.Sanitize());
            throw new DeadLetterException($"Unexpected error creating log entries for facility {request.FacilityId}", ex);
        }
    }

    public async Task ExecuteLogRequest(AcquisitionRequest request, CancellationToken cancellationToken)
    {
        DataAcquisitionLog log = null;
        Activity activity = null;
        Stopwatch stopwatch = null;

        try
        {
            // 1. Get log
            log = await _dataAcquisitionLogQueries.GetCompleteLogAsync(request.logId, cancellationToken);
            if (log == null)
            {
                throw new ArgumentException($"Log with ID {request.logId} does not exist.");
            }

            // Check if facilityId matches
            if (!log.FacilityId.Equals(request.facilityId, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException($"Facility ID {request.facilityId} does not match log's facility ID {log.FacilityId}.");
            }

            activity = new Activity("PatientDataService.ExecuteLogRequest");

            // Set trace parent id based on log trace id
            if (!string.IsNullOrWhiteSpace(log.TraceId))
            {
                try
                {
                    activity.SetParentId(log.TraceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error setting Activity.Current for log ID {LogId} with TraceId {TraceId}", log.Id.Sanitize(), log.TraceId.Sanitize());
                    if (!string.IsNullOrWhiteSpace(Activity.Current?.Id))
                    {
                        activity.SetParentId(Activity.Current.Id);
                    }
                }
            }

            // Helpful attributes for correlation
            activity.AddTag("link.log_id", log.Id.ToString());
            activity.AddTag("link.facility_id", log.FacilityId);
            activity.AddTag("link.correlation_id", log.CorrelationId ?? string.Empty);
            activity.AddTag("link.report_tracking_id", log.ReportTrackingId ?? string.Empty);

            activity.Start();

            // Check if log is flagged as a reference
            if (log.FhirQuery.Any(x => x.isReference.HasValue && x.isReference.Value))
            {
                var nonReferenceLogsCnt = await _dataAcquisitionLogQueries.GetCountOfNonRefLogsIncompleteAsync(
                    log.FacilityId,
                    log.CorrelationId,
                    log.ReportTrackingId,
                    cancellationToken);

                if (nonReferenceLogsCnt > 0 && (log.RetryAttempts ?? 0) < 10)
                {
                    log.Status = RequestStatus.Pending;
                    log.RetryAttempts = (log.RetryAttempts ?? 0) + 1;
                    await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                    return;
                }
                else if ((log.RetryAttempts ?? 0) >= 10)
                {
                    log.Status = RequestStatus.Failed;
                    log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} has exceeded the maximum retry attempts of 10. Not all non-reference resource queries are completed. Marking as Failed.");
                    await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                    throw new DeadLetterException($"Exceeded maximum retry attempts for log ID {log.Id}.");
                }
            }

            // Check retry attempts
            if ((log.RetryAttempts ?? 0) >= 10)
            {
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} has exceeded the maximum retry attempts of 10. Marking as Failed.");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                throw new DeadLetterException($"Exceeded maximum retry attempts for log ID {log.Id}.");
            }

            // Check if log is not in ready state
            if (!request.ignoreStatusConstraint && log.Status != RequestStatus.Ready)
            {
                _logger.LogWarning("Log with ID {LogId} is not in a ready state. Current status: {LogStatus}. Skipping.", log.Id.Sanitize(), log.Status?.GetStringValue());
                log.Status = log.Status == RequestStatus.Completed ? RequestStatus.Completed : RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} is not in a ready state. Current status: {log.Status}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                throw new DeadLetterException($"Log with ID {log.Id} is not in a ready state. Current status: {log.Status}");
            }

            // Check if log has any FhirQuery objects
            if (log.FhirQuery == null || !log.FhirQuery.Any())
            {
                throw new ArgumentException($"Log with ID {log.Id} does not have any FHIR queries defined.");
            }

            // Check if resource types are defined in all FhirQuery objects
            if (log.FhirQuery.Any(x => x.ResourceTypes == null || !x.ResourceTypes.Any()))
            {
                _logger.LogError("Log with ID {LogId} has a FHIR query with no resource types defined.", log.Id.Sanitize());
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} has a FHIR query with no resource types defined.");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                throw new DeadLetterException($"Log with ID {log.Id} has a FHIR query with no resource types defined.");
            }

            // Check if query type is search and there are no query parameters in FhirQuery
            if (log.FhirQuery.Any(x => x.QueryType == FhirQueryType.Search && !x.QueryParameters.Any()))
            {
                _logger.LogError("Log with ID {LogId} has a FHIR query of type 'Search' without any query parameters defined.", log.Id.Sanitize());
                log.Status = RequestStatus.Completed;
                log.CompletionDate = System.DateTime.UtcNow;
                log.CompletionTimeMilliseconds = 0;
                log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} has a FHIR query of type 'Search' without any query parameters defined.");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                return;
            }

            // Set to "Processing"
            await _dataAcquisitionLogManager.UpdateLogStatusAsync(log.Id, RequestStatus.Processing, cancellationToken);

            // Start timer
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // Get FHIR query configuration
            var fhirQueryConfiguration = await _fhirQueryManager.GetAsync(log.FacilityId, cancellationToken);
            if (fhirQueryConfiguration == null)
            {
                throw new MissingFacilityConfigurationException($"No configuration for {log.FacilityId} exists.");
            }

            List<string> resourceIds = new List<string>();

            // Call API
            foreach (var fhirQuery in log.FhirQuery.ToList())
            {
                foreach (var resourceType in fhirQuery.ResourceTypes)
                {
                    if (fhirQuery.QueryType == FhirQueryType.Read)
                    {
                        resourceIds = await _fhirApiService.ExecuteRead(log, fhirQuery, resourceType, fhirQueryConfiguration, resourceIds, cancellationToken);
                    }
                    else if (fhirQuery.QueryType == FhirQueryType.Search)
                    {
                        resourceIds = await _fhirApiService.ExecuteSearch(log, fhirQuery, fhirQueryConfiguration, resourceIds, resourceType, cancellationToken);
                    }
                    else if (fhirQuery.QueryType == FhirQueryType.BulkDataRequest || fhirQuery.QueryType == FhirQueryType.BulkDataPoll)
                    {
                        throw new NotSupportedException("Bulk Data is currently not supported.");
                    }
                }
            }

            // Stop timer and update log
            stopwatch.Stop();
            log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
            log.CompletionDate = System.DateTime.UtcNow;
            log.Status = RequestStatus.Completed;
            log.ResourceAcquiredIds = resourceIds;
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
        }
        catch (DeadLetterException dex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] DeadLetterException: {dex.Message}\n{dex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw;
        }
        catch (TransientException tex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Ready;
                log.RetryAttempts = (log.RetryAttempts ?? 0) + 1;
                log.Notes.Add($"[{DateTime.UtcNow}] TransientException: {tex.Message}. Resetting status to Ready for retry.\n{tex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw;
        }
        catch (ProduceException<string, ResourceAcquired> pex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Ready;
                log.RetryAttempts = (log.RetryAttempts ?? 0) + 1;
                log.Notes.Add($"[{DateTime.UtcNow}] ProduceException: {pex.Message}. Resetting status to Ready for retry.\n{pex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw new TransientException("Error producing ResourceAcquired message", pex);
        }
        catch (MissingFacilityConfigurationException ex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] MissingFacilityConfigurationException: {ex.Message}\n{ex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw new DeadLetterException($"Missing configuration for facility {request.facilityId}", ex);
        }
        catch (NotSupportedException ex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] NotSupportedException: {ex.Message}\n{ex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw new DeadLetterException("Bulk Data operations are not supported", ex);
        }
        catch (Exception ex)
        {
            if (stopwatch != null && stopwatch.IsRunning) stopwatch.Stop();
            if (log != null)
            {
                log.Status = RequestStatus.Failed;
                log.Notes.Add($"[{DateTime.UtcNow}] Unexpected exception: {ex.Message}\n{ex.InnerException}");
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
            }
            throw new DeadLetterException($"Unexpected error in ExecuteLogRequest for log ID {request?.logId ?? "N/A"}", ex);
        }
        finally
        {
            if (activity != null) activity.Stop();
        }
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }
}