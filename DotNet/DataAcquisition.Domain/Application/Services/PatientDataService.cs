using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
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
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
        _fhirApiService = fhirApiService ?? throw new ArgumentNullException(nameof(fhirApiService));
        _distributedSemaphoreProvider = distributedSemaphoreProvider ?? throw new ArgumentNullException(nameof(distributedSemaphoreProvider));
        _patientCensusService = patientCensusService ?? throw new ArgumentNullException(nameof(patientCensusService));
    }

    public async Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var authenticationConfig = await _fhirQueryQueries.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
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

        var referenceTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList();
        referenceTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList());

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

        if (dataAcqRequested == null || string.IsNullOrWhiteSpace(dataAcqRequested.PatientId) || string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ArgumentException("Invalid request data. PatientId and FacilityId must be provided.");
        }

        try
        {
            fhirQueryConfiguration = await _fhirQueryQueries.GetByFacilityIdAsync(request.FacilityId, cancellationToken);

            if (fhirQueryConfiguration == null)
            {
                throw new ArgumentNullException("No FHIR Query Confiugration found for FacilityId: " + request.FacilityId);
            }

            Frequency reportableEventTranslation = ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent(request.ConsumeResult.Value.ReportableEvent);

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

        if (queryPlan != null)
        {
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
                            new CreateDataAcquisitionLogModel
                            {
                                FacilityId = request.FacilityId,
                                CorrelationId = request.CorrelationId,
                                PatientId = request.ConsumeResult.Message.Value.PatientId,
                                ExecutionDate = System.DateTime.UtcNow,
                                Priority = AcquisitionPriority.Normal,
                                ReportableEvent = request.ConsumeResult.Message.Value.ReportableEvent,
                                Status = RequestStatus.Pending,
                                FhirVersion = "R4",
                                QueryType = FhirQueryType.Read,
                                QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Message.Value.QueryType),
                                ScheduledReport = schedReport,
                                TraceId = Activity.Current?.ParentId,
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
                                                QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Message.Value.QueryType),
                                                ResourceType = x.ResourceType,
                                            }).ToList(),
                                        }
                                },
                            }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating log entry for facility {FacilityId} and patient {PatientId}", request.FacilityId.Sanitize(), dataAcqRequested.PatientId);

                        throw;
                    }
                }

                try
                {
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving data from EHR for facility: {FacilityId}", request.FacilityId);
                    throw;
                }

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

        //get semaphore for the facility and logId
        using (_distributedSemaphoreProvider.AcquireSemaphore($"{request.facilityId}-{request.logId}", 1, TimeSpan.FromSeconds(60), cancellationToken: cancellationToken))
        {
            //1. get log
            var log = await _dataAcquisitionLogQueries.GetAsync(request.logId, cancellationToken);

            try
            {
                //check if log is null
                if (log == null)
                {
                    throw new ArgumentException($"Log with ID {request.logId} does not exist.");
                }

                //check to ensure that facilityId matches
                if (!log.FacilityId.Equals(request.facilityId, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException($"Facility ID {request.facilityId} does not match log's facility ID {log.FacilityId}.");
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

                //check if query type is search and there are no query parameters in FhirQuery
                if (log.FhirQuery != null && log.FhirQuery.Any() && log.FhirQuery.Any(x => x.QueryType == FhirQueryType.Search && !x.QueryParameters.Any()))
                {
                    throw new ArgumentException($"Log with ID {log.Id} has a FHIR query of type 'Search' without any query parameters defined.");
                }

                //check if isCensus, if true, create scope for PatientCensusService and execute RetrieveListData
                if (log.IsCensus)
                {
                    await _patientCensusService.RetrieveListData(log, true, cancellationToken);
                    return;
                }

                using var activity = new Activity("PatientDataService.ExecuteLogRequest");

                //set trace parent id based on log trace id
                if (!string.IsNullOrWhiteSpace(log.TraceId))
                {
                    try
                    {
                        activity.SetParentId(log.TraceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error setting Activity.Current for log ID {LogId} with TraceId {TraceId}", log.Id, log.TraceId.Sanitize());
                        if (!string.IsNullOrWhiteSpace(Activity.Current?.Id))
                        {
                            activity.SetParentId(Activity.Current.Id);
                        }
                    }
                }

                // helpful attributes for correlation
                activity.AddTag("link.log_id", log.Id.ToString());
                activity.AddTag("link.facility_id", log.FacilityId);
                activity.AddTag("link.correlation_id", log.CorrelationId ?? string.Empty);
                activity.AddTag("link.report_tracking_id", log.ReportTrackingId ?? string.Empty);

                activity.Start();

                //check if log is flagged as a reference, if yes, check if all non-reference logs for a facility, correlationId, and reportTrackingId are marked as 'Completed'
                if (log.FhirQuery.Any(x => x.IsReference.HasValue && x.IsReference.Value))
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
                        await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                        {
                            Id = log.Id,
                            ResourceAcquiredIds = log.ResourceAcquiredIds,
                            RetryAttempts = log.RetryAttempts,
                            CompletionDate = log.CompletionDate,
                            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                            ExecutionDate = log.ExecutionDate,
                            Notes = log.Notes,
                            Status = log.Status,
                        }, cancellationToken);
                        return;
                    }
                    else if ((log.RetryAttempts ?? 0) >= 10)
                    {
                        log.Notes ??= new List<string>();
                        log.Status = RequestStatus.Failed;
                        log.Notes.Add($"[{DateTime.UtcNow}] Log with ID {log.Id} has exceeded the maximum retry attempts of 10. Not all Non-reference resource queries are completed. Marking as Failed.");
                        await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                        {
                            Id = log.Id,
                            ResourceAcquiredIds = log.ResourceAcquiredIds,
                            RetryAttempts = log.RetryAttempts,
                            CompletionDate = log.CompletionDate,
                            CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                            ExecutionDate = log.ExecutionDate,
                            Notes = log.Notes,
                            Status = log.Status,
                        }, cancellationToken);
                        return;
                    }
                }

                //check if log is not in ready state
                if (!request.ignoreStatusConstraint && log.Status != RequestStatus.Ready)
                {
                    throw new ArgumentException($"Log with ID {log.Id} is not in a ready state. Current status: {log.Status}");
                }

                //2. set to "Processing"
                log.Status = RequestStatus.Processing;
                await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    ResourceAcquiredIds = log.ResourceAcquiredIds,
                    RetryAttempts = log.RetryAttempts,
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate,
                    Notes = log.Notes,
                    Status = log.Status,
                }, cancellationToken);

                //3. start timer
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                //4. get fhir query configuration
                var fhirQueryConfiguration = await _fhirQueryQueries.GetByFacilityIdAsync(log.FacilityId, cancellationToken);

                if (fhirQueryConfiguration == null)
                {
                    throw new MissingFacilityConfigurationException(
                        $"No configuration for {log.FacilityId} exists.");
                }

                //hashset to hold unique resource ids
                var resourceIds = new HashSet<string>();

                bool skipFetch = false;
                
                //4. call api
                foreach (var fhirQuery in log.FhirQuery.ToList())
                {
                    if(skipFetch)
                    {
                        break;
                    }

                    //check if log is search and not census, if true,
                    if (fhirQuery.QueryType == FhirQueryType.Search && !log.IsCensus)
                    {
                        var idParams = fhirQuery.QueryParameters.Where(x => x.StartsWith("_id=", StringComparison.InvariantCultureIgnoreCase)).ToList();
                        if(idParams.Any())
                        {
                            var ids = new List<string>();
                            foreach(var idParam in idParams)
                            {
                                var splitIds = idParam.Substring(4).Trim().Split(',');
                                ids.AddRange(splitIds);
                            }

                            //cleanse ids for empty strings in ids
                            ids = ids.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                            if (!ids.Any())
                            {
                                log.Notes ??= [];
                                log.Notes.Add($"[{DateTime.UtcNow}] No IDs found in _id query parameter for Search FHIR query. Marking log as Completed.");
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
                                var ids = await _fhirApiService.ExecuteRead(log, fhirQuery, resourceType, fhirQueryConfiguration, cancellationToken);
                                if (ids != null) foreach (var id in ids) resourceIds.Add(id);
                            }
                            else if (fhirQuery.QueryType == FhirQueryType.Search)
                            {
                                var ids = await _fhirApiService.ExecuteSearch(log, fhirQuery, fhirQueryConfiguration, resourceType, cancellationToken);
                                if (ids != null) foreach (var id in ids) resourceIds.Add(id);
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
                log.ResourceAcquiredIds = resourceIds.ToList();

                await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    RetryAttempts = log.RetryAttempts,
                    ResourceAcquiredIds = log.ResourceAcquiredIds,
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate,
                    Notes = log.Notes,
                    Status = log.Status,
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PatientDataService.ExecuteLogRequest error");

                log.Notes ??= new List<string>();

                log.Status = RequestStatus.Failed;
                log.Notes.Add($"PatientDataService.ExecuteLogRequest: [{DateTime.UtcNow}] Error encountered: {log.FacilityId?.Sanitize() ?? string.Empty}\n{ex.Message}\n{ex.InnerException?.Message ?? string.Empty}");
                await _dataAcquisitionLogManager.UpdateAsync(new UpdateDataAcquisitionLogModel
                {
                    Id = log.Id,
                    ResourceAcquiredIds = log.ResourceAcquiredIds,
                    RetryAttempts = log.RetryAttempts,
                    CompletionDate = log.CompletionDate,
                    CompletionTimeMilliseconds = log.CompletionTimeMilliseconds, TraceId = log.TraceId,
                    ExecutionDate = log.ExecutionDate,
                    Notes = log.Notes,
                    Status = log.Status,
                }, cancellationToken);

                throw;
            }
        }
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }
}

