using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using StringComparison = System.StringComparison;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IPatientDataService
{
    Task CreateLogEntries(GetPatientDataRequest request, CancellationToken cancellationToken);

    Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request,
        CancellationToken cancellationToken = default);

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
    private readonly IFhirQueryConfigurationQueries _fhirQueryQueries;
    private readonly IQueryPlanQueries _queryPlanQueries;
    private readonly IQueryListProcessor _queryListProcessor;
    private readonly ProducerConfig _producerConfig;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;
    private readonly IFhirApiService _fhirApiService;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;
    private readonly IPatientCensusService _patientCensusService;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationQueries fhirQueryQueries,
        IQueryPlanQueries queryPlanQueries,
        IQueryListProcessor queryListProcessor,
        IReadFhirCommand readFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries,
        IFhirApiService fhirApiService,
        IDistributedSemaphoreProvider distributedSemaphoreProvider,
        IServiceProvider serviceProvider,
        IPatientCensusService patientCensusService)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryQueries = fhirQueryQueries;
        _queryPlanQueries = queryPlanQueries;

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;

        _queryListProcessor = queryListProcessor ?? throw new ArgumentNullException(nameof(queryListProcessor));


        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ??
                                     throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ??
                                     throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _fhirApiService = fhirApiService ?? throw new ArgumentNullException(nameof(fhirApiService));
        _distributedSemaphoreProvider = distributedSemaphoreProvider ??
                                        throw new ArgumentNullException(nameof(distributedSemaphoreProvider));
        _patientCensusService = patientCensusService ?? throw new ArgumentNullException(nameof(patientCensusService));
    }

    public async Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var authenticationConfig =
            await _fhirQueryQueries.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
        var queryConfig = await _fhirQueryQueries.GetByFacilityIdAsync(request.FacilityId, cancellationToken);

        var patient = await _readFhirCommand.ExecuteAsync(
            new ReadFhirCommandRequest(
                request.FacilityId,
                ResourceType.Patient,
                TEMPORARYPatientIdPart(request.ConsumeResult.Value.PatientId),
                queryConfig.FhirServerBaseUrl,
                queryConfig),
            cancellationToken);

        var queryPlan = (await _queryPlanQueries.SearchAsync(new SearchQueryPlanModel
        {
            FacilityId = request.FacilityId
        })).Records.FirstOrDefault();

        if (queryPlan == null)
            throw new MissingFacilityConfigurationException("Query Plan not found.");

        var resources = new List<Resource>();

        var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
        var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

        var referenceTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType)
            .Distinct().ToList();
        referenceTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>()
            .Select(x => x.ResourceType).Distinct().ToList());

        resources.AddRange(await _queryListProcessor.ExecuteFacilityValidationRequest(
            queryPlan.InitialQueries.OrderBy(x => x.Key),
            request,
            queryConfig,
            request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
            queryPlan,
            referenceTypes,
            QueryPlanType.Initial.ToString()));

        resources.AddRange(await _queryListProcessor.ExecuteFacilityValidationRequest(
            queryPlan.SupplementalQueries.OrderBy(x => x.Key),
            request,
            queryConfig,
            request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
            queryPlan,
            referenceTypes,
            QueryPlanType.Supplemental.ToString()));

        return resources;
    }

    public async Task CreateLogEntries(GetPatientDataRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var dataAcqRequested = request.ConsumeResult.Message.Value;

        FhirQueryConfigurationModel? fhirQueryConfiguration = null;
        QueryPlanModel? queryPlan = null;

        if (dataAcqRequested == null || string.IsNullOrWhiteSpace(dataAcqRequested.PatientId) ||
            string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ArgumentException("Invalid request data. PatientId and FacilityId must be provided.");
        }

        try
        {
            fhirQueryConfiguration =
                await _fhirQueryQueries.GetByFacilityIdAsync(request.FacilityId, cancellationToken);

            if (fhirQueryConfiguration == null)
            {
                throw new ArgumentNullException("No FHIR Query Confiugration found for FacilityId: " +
                                                request.FacilityId);
            }

            Frequency reportableEventTranslation =
                ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent(request.ConsumeResult
                    .Value.ReportableEvent);

            queryPlan = (await _queryPlanQueries.SearchAsync(new SearchQueryPlanModel
            {
                FacilityId = request.FacilityId,
                Type = reportableEventTranslation
            })).Records.FirstOrDefault();

            if (fhirQueryConfiguration == null || queryPlan == null)
            {
                throw new MissingFacilityConfigurationException(
                    $"No configuration for {request.FacilityId} exists.");
            }
        }
        catch (MissingFacilityConfigurationException ex)
        {
            _logger.LogError(ex, "Error retrieving configuration for facility {FacilityId}", request.FacilityId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration for facility {FacilityId}", request.FacilityId);
            throw;
        }

        Patient patient = null;
        var patientId = TEMPORARYPatientIdPart(dataAcqRequested.PatientId);

        var traceId = Activity.Current?.TraceId.ToHexString();
        var spanId = Activity.Current?.SpanId.ToHexString();
        var traceAndSpanDelimited = (traceId != null && spanId != null)
            ? $"{traceId}|{spanId}"
            : null;

        if (queryPlan != null)
        {
            var initialQueries =
                queryPlan.InitialQueries.OrderBy(x => int.TryParse(x.Key, out int num) ? num : int.MaxValue);
            var supplementalQueries =
                queryPlan.SupplementalQueries.OrderBy(x => int.TryParse(x.Key, out int num) ? num : int.MaxValue);

            var referenceStrTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>()
                .Select(x => x.ResourceType).Distinct().ToList();
            referenceStrTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>()
                .Select(x => x.ResourceType).Distinct().ToList());

            var referenceTypes = referenceStrTypes.Select(x =>
                new ResourceReferenceType
                {
                    FacilityId = request.FacilityId,
                    QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                    ResourceType = x,
                }).ToList();

            int totalLogsCreated = 0;

            foreach (var schedReport in request.ConsumeResult.Message.Value.ScheduledReports)
            {
                if (request.QueryPlanType == QueryPlanType.Initial)
                {
                    var priority = schedReport.Frequency == Frequency.Daily
                        ? AcquisitionPriority.High
                        : AcquisitionPriority.Normal;

                    try
                    {
                        await _dataAcquisitionLogManager.CreateAsync(
                            new CreateDataAcquisitionLogModel
                            {
                                FacilityId = request.FacilityId,
                                CorrelationId = request.CorrelationId,
                                PatientId = request.ConsumeResult.Message.Value.PatientId,
                                Priority = priority,
                                ExecutionDate = System.DateTime.UtcNow,
                                ReportableEvent = request.ConsumeResult.Message.Value.ReportableEvent,
                                Status = RequestStatus.Pending,
                                FhirVersion = "R4",
                                QueryType = FhirQueryType.Read,
                                QueryPhase =
                                    QueryPhaseUtilities.ToDomain(request.ConsumeResult.Message.Value.QueryType),
                                ScheduledReport = schedReport,
                                TraceId = traceAndSpanDelimited,
                                FhirQuery = new List<CreateFhirQueryModel>
                                {
                                    new CreateFhirQueryModel
                                    {
                                        QueryType = FhirQueryType.Read,
                                        ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                                        QueryParameters = new List<string>(),
                                        FacilityId = request.FacilityId,
                                        ResourceReferenceTypes = referenceTypes.Select(x =>
                                            new CreateResourceReferenceTypeModel
                                            {
                                                FacilityId = request.FacilityId,
                                                QueryPhase =
                                                    QueryPhaseUtilities.ToDomain(request.ConsumeResult.Message.Value
                                                        .QueryType),
                                                ResourceType = x.ResourceType,
                                            }).ToList(),
                                    }
                                },
                            }, cancellationToken);

                        totalLogsCreated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error creating log entry for facility {FacilityId} and patient {PatientId}",
                            request.FacilityId.Sanitize(), dataAcqRequested.PatientId);

                        throw;
                    }
                }

                try
                {
                    totalLogsCreated += await _queryListProcessor.Process(
                        dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase)
                            ? initialQueries
                            : supplementalQueries,
                        request,
                        fhirQueryConfiguration,
                        queryPlan,
                        referenceTypes,
                        dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase)
                            ? QueryPlanType.Initial.ToString()
                            : QueryPlanType.Supplemental.ToString(),
                        schedReport,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving data from EHR for facility: {FacilityId}",
                        request.FacilityId);
                    throw;
                }
            }

            // All logs committed — stamp the sibling count so workers know the full set exists.
            if (totalLogsCreated > 0)
            {
                var queryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType);
                await _dataAcquisitionLogManager.StampSiblingCountAsync(
                    request.FacilityId,
                    request.CorrelationId,
                    queryPhase,
                    totalLogsCreated,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Executes the log request for data acquisition.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="MissingFacilityConfigurationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task ExecuteLogRequest(AcquisitionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        //1. get log
        var log = await _dataAcquisitionLogQueries.GetAsync(request.logId, cancellationToken);

        // Read facility config once — reused by the happy path and all error handlers
        FhirQueryConfigurationModel? fhirQueryConfiguration = null;

        try
        {
            //check if log is null
            if (log == null)
            {
                throw new ArgumentException($"Log with ID {request.logId} does not exist.");
            }

            if (log.ExecutionDate > DateTime.UtcNow)
            {
                throw new ProcessingDelayException(
                    @$"Log Exection Date {log.ExecutionDate} indicates a future processing time.");
            }

            //check to ensure that facilityId matches
            if (!log.FacilityId.Equals(request.facilityId, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ArgumentException(
                    $"Facility ID {request.facilityId} does not match log's facility ID {log.FacilityId}.");
            }

            //check if log has any FhirQuery objects
            if (log.FhirQuery == null || !log.FhirQuery.Any())
            {
                throw new ArgumentException($"Log with ID {log.Id} does not have any FHIR queries defined.");
            }

            //check if resource types are defined in all FhirQuery objects
            if (log.FhirQuery.Any(x => x.ResourceTypes == null || !x.ResourceTypes.Any()))
            {
                throw new ArgumentException($"Log with ID {log.Id} has a FHIR query with no resource types defined.");
            }

            //check if non-reference query type is search and there are no query parameters in FhirQuery
            if (log.FhirQuery != null && log.FhirQuery.Any() &&
                log.FhirQuery.Any(x => x.QueryType == FhirQueryType.Search
                    && !(x.IsReference ?? false)
                    && (x.QueryParameters == null || !x.QueryParameters.Any())))
            {
                throw new ArgumentException(
                    $"Log with ID {log.Id} has a FHIR query of type 'Search' without any query parameters defined.");
            }

            //check if isCensus, if true, create scope for PatientCensusService and execute RetrieveListData
            if (log.IsCensus)
            {
                await _patientCensusService.RetrieveListData(log, true, cancellationToken);
                return;
            }

            ActivityContext parentContext = default;

            if (!string.IsNullOrWhiteSpace(log.TraceId))
            {
                var parts = log.TraceId.Split('|'); // Or whatever delimiter you choose
                if (parts.Length == 2 &&
                    parts[0].Length == 32 && IsValidHex(parts[0]) &&
                    parts[1].Length == 16 && IsValidHex(parts[1]))
                {
                    try
                    {
                        var traceId = ActivityTraceId.CreateFromString(parts[0].AsSpan());
                        var spanId = ActivitySpanId.CreateFromString(parts[1].AsSpan());
                        var traceFlags = ActivityTraceFlags.Recorded;

                        parentContext = new ActivityContext(traceId, spanId, traceFlags);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse combined TraceId/SpanId {TraceId} for log {LogId}",
                            log.TraceId.Sanitize(), log.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid combined TraceId format (expected 32-16 hex chars) for log {LogId}",
                        log.Id);
                }
            }


            using var activity = ServiceActivitySource.Instance.StartActivity(
                "PatientDataService.ExecuteLogRequest",
                ActivityKind.Internal,
                parentContext);

            activity?.SetTag(DiagnosticNames.ReportId, log.Id.ToString());
            activity?.SetTag(DiagnosticNames.FacilityId, log.FacilityId);
            activity?.SetTag(DiagnosticNames.CorrelationId, log.CorrelationId ?? string.Empty);
            activity?.SetTag(DiagnosticNames.ReportTrackingId, log.ReportTrackingId ?? string.Empty);
            activity?.SetTag(DiagnosticNames.PatientId, log.PatientId?.Sanitize());

            //check if log is not in ready state
            if (!request.ignoreStatusConstraint && log.Status != RequestStatus.Queued)
            {
                throw new ArgumentException(
                    $"Log with ID {log.Id} is not in a queued state. Current status: {log.Status}");
            }

            //2. atomically update to "Processing" — single DB write, no follow-up UpdateAsync needed
            var allowedStatuses = new List<RequestStatus> { RequestStatus.Queued };
            if (request.ignoreStatusConstraint && log.Status.HasValue)
            {
                allowedStatuses.Add(log.Status.Value);
            }

            var successfullyUpdatedLog = await _dataAcquisitionLogManager.TrySetLogStatusAsync(log.Id,
                allowedStatuses, RequestStatus.Processing,
                cancellationToken);

            if (successfullyUpdatedLog)
            {
                log.Status = RequestStatus.Processing;

                //3. start timer
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                //4. get fhir query configuration (read once, reused by error handlers)
                fhirQueryConfiguration =
                    await _fhirQueryQueries.GetByFacilityIdAsync(log.FacilityId, cancellationToken);

                if (fhirQueryConfiguration == null)
                {
                    throw new MissingFacilityConfigurationException(
                        $"No configuration for {log.FacilityId} exists.");
                }

                var maxRetryAttempts = fhirQueryConfiguration.MaxRetries ?? DataAcquisitionLog.MaxRetryAttempts;

                //hashset to hold unique resource ids
                var resourceIds = new HashSet<string>();

                bool skipFetch = false;

                var newNotes = new List<string>();

                //4. call api
                foreach (var fhirQuery in log.FhirQuery.ToList())
                {
                    if (skipFetch)
                    {
                        break;
                    }

                    //check if log is search and not census, if true,
                    if ((fhirQuery.QueryType == FhirQueryType.Search ||
                         fhirQuery.QueryType == FhirQueryType.SearchPost) && !log.IsCensus)
                    {
                        var idParams = fhirQuery.QueryParameters?
                            .Where(x => x.StartsWith("_id=", StringComparison.InvariantCultureIgnoreCase)).ToList() ?? [];
                        if (idParams.Any())
                        {
                            var ids = new List<string>();
                            foreach (var idParam in idParams)
                            {
                                var splitIds = idParam.Substring(4).Trim().Split(',');
                                ids.AddRange(splitIds);
                            }

                            //cleanse ids for empty strings in ids
                            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                            if (!ids.Any())
                            {
                                newNotes.Add(
                                    $"[{DateTime.UtcNow}] No IDs found in _id query parameter for {fhirQuery.QueryType} FHIR query. Marking log as Completed.");
                                skipFetch = true;
                            }
                        }
                    }

                    if (!skipFetch)
                    {
                        foreach (var resourceType in fhirQuery.ResourceTypes)
                        {
                            if (fhirQuery.QueryType == FhirQueryType.Read)
                            {
                                var ids = await _fhirApiService.ExecuteRead(log, fhirQuery, resourceType,
                                    fhirQueryConfiguration, cancellationToken);
                                if (ids != null)
                                    foreach (var id in ids)
                                        resourceIds.Add(id);
                            }
                            else if (fhirQuery.QueryType == FhirQueryType.Search ||
                                     fhirQuery.QueryType == FhirQueryType.SearchPost)
                            {
                                var ids = await _fhirApiService.ExecuteSearch(log, fhirQuery, fhirQueryConfiguration,
                                    resourceType, cancellationToken);
                                if (ids != null)
                                    foreach (var id in ids)
                                        resourceIds.Add(id);
                            }
                            else if (fhirQuery.QueryType == FhirQueryType.BulkDataRequest)
                            {
                                throw new NotSupportedException("Bulk Data is currently not supported.");
                            }
                            else if (fhirQuery.QueryType == FhirQueryType.BulkDataPoll)
                            {
                                throw new NotSupportedException("Bulk Data is currently not supported.");
                            }
                        }
                    }
                }

                //5. stop timer and update log
                stopwatch.Stop();

                log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
                log.CompletionDate = System.DateTime.UtcNow;
                log.Status = skipFetch ? RequestStatus.Skipped : RequestStatus.Completed;

                await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    RetryAttempts = log.RetryAttempts,
                    ResourceAcquiredIds = resourceIds.ToList(),
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                    TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate,
                    NewNotes = newNotes.Count > 0 ? newNotes : null,
                    Status = log.Status,
                }, cancellationToken);
            }
        }
        catch (OpOutcomeException ex)
        {
            _logger.LogWarning(ex, "OperationOutcome encountered for facility {FacilityId}", log.FacilityId.Sanitize());

            string? newNote = null;

            if (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                log.Status = RequestStatus.Completed;
                log.CompletionDate = DateTime.UtcNow;
            }
            else
            {
                var maxRetryAttempts = fhirQueryConfiguration?.MaxRetries ?? DataAcquisitionLog.MaxRetryAttempts;

                log.RetryAttempts ??= 0;
                log.RetryAttempts++;

                if (log.RetryAttempts >= maxRetryAttempts)
                {
                    log.Status = RequestStatus.MaxRetriesReached;
                    newNote = $"[{DateTime.UtcNow}] OperationOutcome encountered (HTTP {ex.StatusCode}): Maximum retry attempts reached ({maxRetryAttempts}).";
                }
                else
                {
                    log.Status = RequestStatus.Failed;
                    newNote = $"[{DateTime.UtcNow}] OperationOutcome encountered (HTTP {ex.StatusCode}): Retrying. Attempt {log.RetryAttempts}.";
                }
            }

            await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
            {
                Id = log.Id,
                RetryAttempts = log.RetryAttempts,
                CompletionDate = log.CompletionDate,
                CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                TraceId = log.TraceId,
                ExecutionDate = log.ExecutionDate,
                NewNotes = newNote != null ? [newNote] : null,
                Status = log.Status,
            }, cancellationToken);
        }
        catch (ProcessingDelayException ex)
        {
            log!.RetryAttempts ??= 0;

            log.Status = RequestStatus.Pending;
            var newNote = $"[{DateTime.UtcNow}] Processing delay encountered. Retrying at {log.ExecutionDate}. See application logs for details.";

            await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
            {
                Id = log.Id,
                RetryAttempts = log.RetryAttempts,
                ExecutionDate = log.ExecutionDate,
                Status = log.Status,
                NewNotes = [newNote],
                CompletionDate = log.CompletionDate,
                CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                TraceId = log.TraceId
            }, cancellationToken);
        }
        catch (TooManyRequestsException ex)
        {
            _logger.LogWarning(ex, "Throttled by 429 for facility {FacilityId}", log.FacilityId.Sanitize());

            log.RetryAttempts ??= 0;

            log.ExecutionDate = DateTime.UtcNow.Add(ex.RetryAfter);
            log.Status = RequestStatus.Failed; //Don't count this as a failure
            var newNote =
                $"[{DateTime.UtcNow}] Throttled (429): Retrying after {ex.RetryAfter.TotalSeconds}s. Attempt {log.RetryAttempts}.";

            await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
            {
                Id = log.Id,
                RetryAttempts = log.RetryAttempts,
                ExecutionDate = log.ExecutionDate,
                Status = log.Status,
                NewNotes = [newNote],
                CompletionDate = log.CompletionDate,
                CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                TraceId = log.TraceId
            }, cancellationToken);

            await _dataAcquisitionLogManager.ThrottleFacilityAcquisitions(log.FacilityId, log.ExecutionDate.Value,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PatientDataService.ExecuteLogRequest error");

            var maxRetryAttempts = fhirQueryConfiguration?.MaxRetries ?? DataAcquisitionLog.MaxRetryAttempts;

            log.RetryAttempts ??= 0;
            log.RetryAttempts++;

            string newNote;
            if (log.RetryAttempts >= maxRetryAttempts)
            {
                log.Status = RequestStatus.MaxRetriesReached;
                newNote =
                    $"[{DateTime.UtcNow}] Error encountered. Maximum retry attempts reached ({maxRetryAttempts}). See application logs for details.";
            }
            else
            {
                log.Status = RequestStatus.Failed;
                newNote =
                    $"[{DateTime.UtcNow}] Error encountered. Retrying. Attempt {log.RetryAttempts}. See application logs for details.";
            }

            await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
            {
                Id = log.Id,
                RetryAttempts = log.RetryAttempts,
                CompletionDate = log.CompletionDate,
                CompletionTimeMilliseconds = log.CompletionTimeMilliseconds,
                TraceId = log.TraceId,
                ExecutionDate = log.ExecutionDate,
                NewNotes = [newNote],
                Status = log.Status,
            }, cancellationToken);

            throw;
        }
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }

    private static bool IsValidHex(string s)
    {
        foreach (char c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }

        return true;
    }
}
