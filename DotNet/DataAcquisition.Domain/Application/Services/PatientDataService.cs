using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

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
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IQueryListProcessor _queryListProcessor;
    private readonly ProducerConfig _producerConfig;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IDataAcquisitionLogQueries _dataAcquisitionLogQueries;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationManager fhirQueryManager,
        IQueryPlanManager queryPlanManager,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IQueryListProcessor queryListProcessor,
        IReadFhirCommand readFhirCommand,
        ISearchFhirCommand searchFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IReferenceResourcesManager referenceResourcesManager,
        IDataAcquisitionLogQueries dataAcquisitionLogQueries)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));

        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;

        _queryListProcessor = queryListProcessor ?? throw new ArgumentNullException(nameof(queryListProcessor));


        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _searchFhirCommand = searchFhirCommand ?? throw new ArgumentNullException(nameof(searchFhirCommand));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _dataAcquisitionLogQueries = dataAcquisitionLogQueries ?? throw new ArgumentNullException(nameof(dataAcquisitionLogQueries));
    }

    public async Task<List<Resource>> ValidateFacilityConnection(GetPatientDataRequest request, CancellationToken cancellationToken = default)
    {
        if(request == null)
            throw new ArgumentNullException(nameof(request));

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

        var queryPlan = (
            await _queryPlanManager.FindAsync(
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

        FhirQueryConfiguration fhirQueryConfiguration = null;
        QueryPlan? queryPlan = null;

        if (dataAcqRequested == null || string.IsNullOrWhiteSpace(dataAcqRequested.PatientId) || string.IsNullOrWhiteSpace(request.FacilityId))
        {
            throw new ArgumentException("Invalid request data. PatientId and FacilityId must be provided.");
        }

        try
        {
            fhirQueryConfiguration = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);
            Frequency reportableEventTranslation = ReportableEventToQueryPlanTypeFactory.GenerateQueryPlanTypeFromReportableEvent(request.ConsumeResult.Value.ReportableEvent);
            queryPlan = (await _queryPlanManager.FindAsync(
                q => q.FacilityId == request.FacilityId 
                    && q.Type == reportableEventTranslation
                , cancellationToken))
                ?.FirstOrDefault();

            if (fhirQueryConfiguration == null || queryPlan == null)
            {
                throw new MissingFacilityConfigurationException(
                    $"No configuration for {request.FacilityId} exists.");
            }
        }
        catch (MissingFacilityConfigurationException ex)
        {
            var message =
                $"Error retrieving configuration for facility {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
            _logger.LogError(message);
            throw;
        }
        catch (Exception ex)
        {
            var message =
                $"Error retrieving configuration for facility {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
            _logger.LogError(message);
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
                foreach (var measure in schedReport.ReportTypes)
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
                                    Status = RequestStatus.Pending,
                                    ReportEndDate = schedReport.EndDate,
                                    ReportStartDate = schedReport.StartDate,
                                    QueryType = FhirQueryType.Read,
                                    QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Message.Value.QueryType),
                                    ScheduledReport = schedReport,
                                    TimeZone = fhirQueryConfiguration.TimeZone,
                                    FhirQuery = new List<FhirQuery>
                                    {
                                        new FhirQuery
                                        {
                                            QueryType = FhirQueryType.Read,
                                            ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                                            QueryParameters = new List<string>(),
                                            MeasureId = measure,
                                            FacilityId = request.FacilityId,
                                            ResourceReferenceTypes = referenceTypes.Select(x =>
                                            new ResourceReferenceType
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
                            var message = "Error creating log entry for facility {request.FacilityId} and patient {dataAcqRequested.PatientId}\n{ex.Message}\n{ex.InnerException}";
                            _logger.LogError(ex, message, request.FacilityId, dataAcqRequested.PatientId);

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
                    catch (ProduceException<string, ResourceAcquired>)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var message =
                            $"Error retrieving data from EHR for facility: {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
                        _logger.LogError(message);
                        throw;
                    }
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
    public async Task ExecuteLogRequest(AcquisitionRequest request, CancellationToken cancellationToken) 
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        //1. get log
        var log = await _dataAcquisitionLogQueries.GetCompleteLogAsync(request.logId, cancellationToken);

        //check to ensure that facilityId matches
        if (!log.FacilityId.Equals(request.facilityId, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new ArgumentException($"Facility ID {request.facilityId} does not match log's facility ID {log.FacilityId}.");
        }

        //check if log is not in pending state
        if (!request.ignoreStatusConstraint && log.Status != RequestStatus.Ready)
        {
            _logger.LogWarning("Log with ID {log.Id} is not in a ready state. Current status: {log.Status}.Skipping.", log.Id, log.Status?.GetStringValue());
            log.Status = log.Status == RequestStatus.Completed ? RequestStatus.Completed : RequestStatus.Failed;
            log.Notes.Add($"Log with ID {log.Id} is not in a ready state. Current status: {log.Status}");
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

            throw new ArgumentException($"Log with ID {log.Id} is not in a ready state. Current status: {log.Status}");
        }

        //check if log has any FhirQuery objects
        if (log.FhirQuery == null || !log.FhirQuery.Any())
        {
            throw new ArgumentException($"Log with ID {log.Id} does not have any FHIR queries defined.");
        }

        //2. set to "Processing"
        log.Status = RequestStatus.Processing;
        await _dataAcquisitionLogManager.UpdateLogStatusAsync(log.Id, RequestStatus.Processing, cancellationToken);

        //3. start timer
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        //4. get fhir query configuration
        var fhirQueryConfiguration = await _fhirQueryManager.GetAsync(log.FacilityId, cancellationToken);

        if (fhirQueryConfiguration == null)
        {
            throw new MissingFacilityConfigurationException(
                $"No configuration for {log.FacilityId} exists.");
        }

        List<string> resourceIds = new List<string>();

        //4. call api
        foreach (var fhirQuery in log.FhirQuery)
        {
            foreach (var resourceType in fhirQuery.ResourceTypes)
            {
                
                if (fhirQuery.QueryType == FhirQueryType.Read)
                {

                    try
                    {
                        var resource = await _readFhirCommand.ExecuteAsync(
                                        new ReadFhirCommandRequest(
                                            log.FacilityId,
                                            resourceType,
                                            resourceType == ResourceType.Patient ? log.PatientId : log.ResourceId,
                                            fhirQueryConfiguration.FhirServerBaseUrl,
                                            fhirQueryConfiguration),
                                        cancellationToken);

                        resourceIds.Add(resource.Id);

                        await GenerateResourceAcquiredMessage(new ResourceAcquired
                        {
                            Resource = resource,
                            ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                            PatientId = log.PatientId,
                            QueryType = log.QueryPhase.ToString(),
                        }, log.FacilityId, log.CorrelationId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        log.Status = RequestStatus.Failed;
                        log.Notes.Add($"Error retrieving data from EHR for facility: {log.FacilityId}\n{ex.Message}\n{ex.InnerException}");
                        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
                        throw;
                    }

                }
                else if (fhirQuery.QueryType == FhirQueryType.Search)
                {
                    if(fhirQuery.isReference.HasValue && fhirQuery.isReference.Value)
                    {
                        var references = await _referenceResourcesManager.GetReferencesByFacilityAndLogId(log.FacilityId, log.Id, cancellationToken);
                        
                        foreach(var reference in references)
                        {
                            var paramLst = new List<string> { $"_id={reference.Id}" }.Concat(fhirQuery.QueryParameters).ToList();
                            var sParams = BuildSearchParams(paramLst);

                            Bundle refBundle = null;
                            if (reference.ReferenceResource == null)
                            {
                                refBundle = await _searchFhirCommand.ExecuteNonPagingAsync(
                                new SearchFhirCommandRequest
                                (
                                    fhirQueryConfiguration,
                                    Enum.Parse<ResourceType>(reference.ResourceType),
                                    sParams,
                                    log.FacilityId,
                                    log.PatientId,
                                    log.CorrelationId,
                                    log.QueryPhase
                                    ),
                                cancellationToken);

                                reference.ReferenceResource = System.Text.Json.JsonSerializer.Serialize<DomainResource>((DomainResource)refBundle.Entry.FirstOrDefault().Resource, new System.Text.Json.JsonSerializerOptions().ForFhir());
                                try
                                {
                                    await _referenceResourcesManager.UpdateAsync(reference, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    log.Status = RequestStatus.Failed;
                                    log.Notes.Add($"Error updating reference resource for facility: {log.FacilityId}\n{ex.Message}\n{ex.InnerException}");
                                    await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

                                    throw;
                                }
                            }
                            else
                            {
                                var deSerResource = FhirResourceDeserializer.DeserializeFhirResource(reference);
                                refBundle = new Bundle
                                {
                                    Id = deSerResource.Id,
                                    Type = Bundle.BundleType.Document,
                                    Entry = new List<Bundle.EntryComponent>
                                    {
                                        new Bundle.EntryComponent
                                        {
                                            Resource = deSerResource
                                        }
                                    }
                                };
                            }

                            resourceIds.AddRange(refBundle.Entry.Select(x => x.Resource.Id).ToList());

                            foreach(var entry in refBundle.Entry)
                            {
                                await GenerateResourceAcquiredMessage(new ResourceAcquired
                                {
                                    Resource = entry.Resource,
                                    ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                                    PatientId = log.PatientId,
                                    QueryType = log.QueryPhase.ToString(),
                                }, log.FacilityId, log.CorrelationId, cancellationToken);
                            }
                            
                        }
                    }
                    else
                    {
                        var searchParams = BuildSearchParams(fhirQuery.QueryParameters);

                        try
                        {
                            await foreach (var bundle in _searchFhirCommand.ExecuteAsync(
                                            new SearchFhirCommandRequest(
                                                fhirQueryConfiguration,
                                            resourceType,
                                            searchParams,
                                            log.FacilityId,
                                            log.PatientId,
                                            log.CorrelationId,
                                            log.QueryPhase),
                                            cancellationToken))
                            {
                                var refResources = ReferenceResourceBundleExtractor.Extract(bundle, fhirQuery.ResourceReferenceTypes.Select(x => x.ResourceType).ToList());

                                //save reference resources to db
                                foreach (var refResource in refResources)
                                {
                                    var existingRef = await _referenceResourcesManager.GetByResourceIdAndFacilityId(refResource.Reference.SplitReference(), log.FacilityId, cancellationToken);

                                    if (existingRef == null || existingRef.ReferenceResource == null)
                                    {
                                        var referenceResource = new ReferenceResources
                                        {
                                            ResourceId = refResource.Reference.SplitReference(),
                                            ResourceType = refResource.Type,
                                            FacilityId = log.FacilityId,
                                            DataAcquisitionLogId = log.Id,
                                        };
                                        await _referenceResourcesManager.AddAsync(referenceResource, cancellationToken);
                                    }
                                }

                                var resources = bundle.Entry.Select(e => e.Resource).ToList();
                                resourceIds.AddRange(resources.Select(r => r.Id));

                                foreach (var resource in resources)
                                {
                                    await GenerateResourceAcquiredMessage(new ResourceAcquired
                                    {
                                        Resource = resource,
                                        ScheduledReports = new List<ScheduledReport> { log.ScheduledReport },
                                        PatientId = log.PatientId,
                                        QueryType = log.QueryPhase.ToString(),
                                    }, log.FacilityId, log.CorrelationId, cancellationToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Status = RequestStatus.Failed;
                            log.Notes.Add($"Error retrieving data from EHR for facility: {log.FacilityId}\n{ex.Message}\n{ex.InnerException}");
                            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

                            throw;
                        }
                    }               
                }
                else if (fhirQuery.QueryType == FhirQueryType.BulkDataRequest) { throw new NotSupportedException("Bulk Data is currently not supported."); }
                else if (fhirQuery.QueryType == FhirQueryType.BulkDataPoll) { throw new NotSupportedException("Bulk Data is currently not supported."); }
            }
        }

        //5. stop timer and update log
        stopwatch.Stop();

        log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
        log.CompletionDate = System.DateTime.UtcNow;
        log.Status = RequestStatus.Completed;
        log.ResourceAcquiredIds = resourceIds;
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
    }
    
    private SearchParams BuildSearchParams(List<string> parameters)
    {
        var searchParams = new SearchParams();
        foreach (var param in parameters)
        {
            var splitParams = param.Split('=');
            if (splitParams.Length != 2)
            {
                throw new ArgumentException($"Invalid search parameter format: {param}");
            }
            searchParams.Add(splitParams[0], splitParams[1]);
        }
        return searchParams;
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }

    private async Task GenerateResourceAcquiredMessage(ResourceAcquired resourceAcquired, string facilityId, string correlationId, CancellationToken cancellationToken = default)
    {
        await _kafkaProducer.ProduceAsync(
                    KafkaTopic.ResourceAcquired.ToString(),
                    new Message<string, ResourceAcquired>
                    {
                        Key = facilityId,
                        Headers = new Headers
                        {
                                new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
                        },
                        Value = resourceAcquired
                    }, cancellationToken);
        _kafkaProducer.Flush(cancellationToken);
    }

    private bool CheckQueryPlanForPatientType(Dictionary<string, IQueryConfig> queries)
    {
        return queries.Any(x =>
        {
            if (x.Value is ReferenceQueryConfig referenceQueryConfig)
            {
                return referenceQueryConfig.ResourceType == ResourceType.Patient.ToString();
            }
            else if (x.Value is ParameterQueryConfig parameterQueryConfig)
            {
                return parameterQueryConfig.ResourceType == ResourceType.Patient.ToString();
            }
            return false;
        });
    }
}
