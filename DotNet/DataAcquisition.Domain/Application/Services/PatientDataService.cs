using Confluent.Kafka;
using DataAcquisition.Domain;
using DataAcquisition.Domain.Application.Models;
using DataAcquisition.Domain.Entities;
using DataAcquisition.Domain.Models.Enums;
using Hl7.Fhir.ElementModel.Types;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IPatientDataService
{
    Task CreateLogEntries(GetPatientDataRequest request, CancellationToken cancellationToken);
    Task<List<Resource>> Get_NoKafka(GetPatientDataRequest request, CancellationToken cancellationToken = default);
    Task Get(AcquisitionRequest request, CancellationToken cancellationToken);
}

public class PatientDataService : IPatientDataService
{
    private readonly IDatabase _database;

    private readonly ILogger<PatientDataService> _logger;
    private readonly IFhirQueryConfigurationManager _fhirQueryManager;
    private readonly IQueryPlanManager _queryPlanManager;
    private readonly IFhirApiService _fhirRepo;
    private readonly IProducer<string, ResourceAcquired> _kafkaProducer;
    private readonly IQueryListProcessor _queryListProcessor;
    private readonly ProducerConfig _producerConfig;
    private readonly IReadFhirCommand _readFhirCommand;
    private readonly ISearchFhirCommand _searchFhirCommand;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IReferenceResourcesManager _referenceResourcesManager;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationManager fhirQueryManager,
        IQueryPlanManager queryPlanManager,
        IFhirApiService fhirRepo,
        IProducer<string, ResourceAcquired> kafkaProducer,
        IQueryListProcessor queryListProcessor,
        IReadFhirCommand readFhirCommand,
        ISearchFhirCommand searchFhirCommand,
        IDataAcquisitionLogManager dataAcquisitionLogManager,
        IReferenceResourcesManager referenceResourcesManager)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));

        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;

        _queryListProcessor = queryListProcessor ?? throw new ArgumentNullException(nameof(queryListProcessor));


        _readFhirCommand = readFhirCommand ?? throw new ArgumentNullException(nameof(readFhirCommand));
        _searchFhirCommand = searchFhirCommand ?? throw new ArgumentNullException(nameof(searchFhirCommand));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
    }

    public async Task<List<Resource>> Get_NoKafka(GetPatientDataRequest request, CancellationToken cancellationToken = default)
    {
        var authenticationConfig = await _fhirQueryManager.GetAuthenticationConfigurationByFacilityId(request.FacilityId, cancellationToken);
        var queryConfig = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);
        var patient = await _fhirRepo.GetPatient(
            queryConfig.FhirServerBaseUrl,
            request.ConsumeResult.Value.PatientId,
            Guid.NewGuid().ToString(),
            request.FacilityId,
            authenticationConfig,
            request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
            cancellationToken) ?? throw new NotFoundException("Patient not found.");
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

        resources.AddRange(await _queryListProcessor.Process_NoKafka(
                queryPlan.InitialQueries.OrderBy(x => x.Key),
                request,
                queryConfig,
                request.ConsumeResult.Value.ScheduledReports.FirstOrDefault(),
                queryPlan,
                referenceTypes,
                QueryPlanType.Initial.ToString()));

        resources.AddRange(await _queryListProcessor.Process_NoKafka(
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
        var dataAcqRequested = request.ConsumeResult.Message.Value;

        FhirQueryConfiguration fhirQueryConfiguration = null;
        QueryPlan? queryPlan = null;

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
        bool createPatientLog = dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase);

        if (queryPlan != null)
        {
            var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
            var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

            var referenceTypes = queryPlan.InitialQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList();
            referenceTypes.AddRange(queryPlan.SupplementalQueries.Values.OfType<ReferenceQueryConfig>().Select(x => x.ResourceType).Distinct().ToList());

            if (createPatientLog)
            {
                foreach (var schedReport in request.ConsumeResult.Value.ScheduledReports)
                {
                    foreach (var measure in schedReport.ReportTypes)
                    {
                        await _dataAcquisitionLogManager.CreateAsync(
                        new DataAcquisitionLog
                        {
                            FacilityId = request.FacilityId,
                            CorrelationId = request.CorrelationId,
                            PatientId = request.ConsumeResult.Value.PatientId,
                            ExecutionDate = System.DateTime.UtcNow,
                            Priority = AcquisitionPriority.Normal,
                            QueryType = FhirQueryType.Read,
                            QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                            ScheduledReport = schedReport,
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
                                        QueryPhase = QueryPhaseUtilities.ToDomain(request.ConsumeResult.Value.QueryType),
                                        ResourceType = x,
                                    }).ToList(),
                                }
                            },
                        }, cancellationToken);
                    }
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
                        dataAcqRequested.QueryType.Equals("Initial", System.StringComparison.InvariantCultureIgnoreCase) ? QueryPlanType.Initial.ToString() : QueryPlanType.Supplemental.ToString(), cancellationToken);

            }
            catch (ProduceException<string, ResourceAcquired>)
            {
                throw;
            }
            catch (Exception ex)
            {
                //produce tailing message
                await ProduceTailingMessage(request.FacilityId, request.CorrelationId, patientId, dataAcqRequested.QueryType, dataAcqRequested.ScheduledReports, cancellationToken);

                var message =
                    $"Error retrieving data from EHR for facility: {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
                _logger.LogError(message);
                throw;
            }
        }

        //produce tailing message to indicate acquisition is complete
        await ProduceTailingMessage(request.FacilityId, request.CorrelationId, patientId, dataAcqRequested.QueryType, dataAcqRequested.ScheduledReports, cancellationToken);
    }

    public async Task Get(AcquisitionRequest request, CancellationToken cancellationToken) 
    {
        //1. get log
        var log = await _dataAcquisitionLogManager.GetAsync(request.logId, cancellationToken);

        //2. set to "Processing"
        log.Status = Domain.Models.Enums.RequestStatus.Processing;
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

        //3. start timer
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        //check if potential reference resource
        var isPotentialReferenceResource = log.FhirQuery.FirstOrDefault().ResourceReferenceTypes
            .Any(x => x.FacilityId == log.FacilityId && x.QueryPhase == log.QueryPhase && log.FhirQuery.FirstOrDefault().ResourceTypes.Any(y => y.ToString() == x.ResourceType));

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
            if (fhirQuery.QueryType == FhirQueryType.Read)
            {
                foreach(var resourceType in fhirQuery.ResourceTypes)
                {
                    var resource = await _readFhirCommand.ExecuteAsync(
                    log.FacilityId,
                    resourceType,
                    resourceType == ResourceType.Patient ? log.PatientId : log.ResourceId,
                    fhirQueryConfiguration.FhirServerBaseUrl,
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
            }
            else if (fhirQuery.QueryType == FhirQueryType.Search)
            {
                var searchParams = new SearchParams();
                foreach (var param in fhirQuery.QueryParameters)
                {
                    var splitParams = param.Split('=');
                    if (splitParams.Length != 2)
                    {
                        throw new ArgumentException($"Invalid search parameter format: {param}");
                    }
                    searchParams.Add(splitParams[0], splitParams[1]);
                }

                foreach (var resourceType in fhirQuery.ResourceTypes)
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
            }
            else if (log.FhirQuery.FirstOrDefault().QueryType == FhirQueryType.BulkDataRequest) { throw new NotSupportedException("Bulk Data is currently not supported."); }
            else if (log.FhirQuery.FirstOrDefault().QueryType == FhirQueryType.BulkDataPoll) { throw new NotSupportedException("Bulk Data is currently not supported."); }
        }

        

        //5. stop timer and update log
        stopwatch.Stop();

        log.CompletionTimeMilliseconds = stopwatch.ElapsedMilliseconds;
        log.CompletionDate = System.DateTime.UtcNow;
        log.Status = Domain.Models.Enums.RequestStatus.Completed;
        log.ResourceAcquiredIds = resourceIds;
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);
    }
    
    private async Task ProduceTailingMessage(string facilityId, string correlationId, string patientId, string queryType, List<ScheduledReport> scheduledReports, CancellationToken cancellationToken)
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
                Value = new ResourceAcquired
                {
                    AcquisitionComplete = true,
                    PatientId = patientId,
                    QueryType = queryType,
                    ScheduledReports = scheduledReports
                }
            }, cancellationToken);
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
    }
}
