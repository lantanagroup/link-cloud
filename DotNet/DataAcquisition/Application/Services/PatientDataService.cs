using Confluent.Kafka;
using DataAcquisition.Domain;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Application.Services;

public interface IPatientDataService
{
    System.Threading.Tasks.Task Get(GetPatientDataRequest request, CancellationToken cancellationToken);
}

public class PatientDataService : IPatientDataService
{
    private readonly IDatabase _database;

    private readonly ILogger<PatientDataService> _logger;
    private readonly IFhirQueryConfigurationManager _fhirQueryManager;
    private readonly IQueryPlanManager _queryPlanManager;
    private readonly IReferenceResourcesManager _referenceResourcesManager;
    private readonly IFhirApiService _fhirRepo;
    private readonly IKafkaProducerFactory<string, ResourceAcquired> _kafkaProducerFactory;
    private readonly IReferenceResourceService _referenceResourceService;
    private readonly ProducerConfig _producerConfig;

    public PatientDataService(
        IDatabase database,
        ILogger<PatientDataService> logger,
        IFhirQueryConfigurationManager fhirQueryManager,
        IQueryPlanManager queryPlanManager,
        IReferenceResourcesManager referenceResourcesManager,
        IFhirApiService fhirRepo,
        IKafkaProducerFactory<string, ResourceAcquired> kafkaProducerFactory,
        IReferenceResourceService referenceResourceService)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _queryPlanManager = queryPlanManager ?? throw new ArgumentNullException(nameof(queryPlanManager));
        _referenceResourcesManager = referenceResourcesManager ?? throw new ArgumentNullException(nameof(referenceResourcesManager));
        _referenceResourceService = referenceResourceService ?? throw new ArgumentNullException(nameof(referenceResourceService));

        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));

        _producerConfig = new ProducerConfig();
        _producerConfig.CompressionType = CompressionType.Zstd;
        
    }

    public async Task Get(GetPatientDataRequest request, CancellationToken cancellationToken)
    {
        var dataAcqRequested = request.ConsumeResult.Message.Value;

        FhirQueryConfiguration fhirQueryConfiguration = null;
        List<QueryPlan> queryPlans = null;

        try
        {
            fhirQueryConfiguration = await _fhirQueryManager.GetAsync(request.FacilityId, cancellationToken);
            queryPlans =
                await _queryPlanManager.FindAsync(q => q.FacilityId == request.FacilityId, cancellationToken);

            if (fhirQueryConfiguration == null || queryPlans == null)
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

        foreach (var scheduledReport in dataAcqRequested.ScheduledReports)
        {
            var patientId = TEMPORARYPatientIdPart(dataAcqRequested.PatientId);

            Patient patient = null;

            if (dataAcqRequested.QueryType.Equals("Initial", StringComparison.InvariantCultureIgnoreCase))
            {
                patient = await _fhirRepo.GetPatient(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    patientId, request.CorrelationId,
                    request.FacilityId,
                    fhirQueryConfiguration.Authentication, cancellationToken);

                _kafkaProducerFactory.CreateProducer(_producerConfig).Produce(
                    KafkaTopic.ResourceAcquired.ToString(), 
                    new Message<string, ResourceAcquired>
                    {
                        Key = request.FacilityId,
                        Headers = new Headers
                        {
                            new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(request.CorrelationId))
                        },
                        Value = new ResourceAcquired
                        {
                            Resource = patient,
                            ScheduledReports = new List<ScheduledReport> { scheduledReport },
                            PatientId = patientId,
                            QueryType = dataAcqRequested.QueryType
                        }
                    });
            }

            var queryPlan = queryPlans.FirstOrDefault(x => x.ReportType == scheduledReport.ReportType);

            if (queryPlan != null)
            {
                var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
                var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);

                try
                {
                    await ProcessIQueryList(
                            dataAcqRequested.QueryType == "Initial" ? initialQueries : supplementalQueries,
                            request,
                            fhirQueryConfiguration,
                            scheduledReport,
                            queryPlan,
                            dataAcqRequested.QueryType == "Initial" ? QueryPlanType.InitialQueries.ToString() : QueryPlanType.SupplementalQueries.ToString());

                }
                catch(ProduceException<string, ResourceAcquired>)
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

    private bool RemovePatientId(Resource resource)
    {
        return resource switch
        {
            Device => true,
            Medication => true,
            Location => true,
            Specimen => true,
            _ => false,
        };
    }

    private async Task GenerateMessagesFromBundle(
        Bundle bundle, 
        string patientId, 
        string queryType,
        string correlationId,
        List<ScheduledReport> scheduledReports,
        CancellationToken cancellationToken)
    {
        bundle.Entry.ForEach(e => 
        {
            if (e.Resource is Resource resource)
            {
                _kafkaProducerFactory.CreateProducer(_producerConfig).Produce(
                    KafkaTopic.ResourceAcquired.ToString(),
                    new Message<string, ResourceAcquired>
                    {
                        Key = patientId,
                        Headers = new Headers
                        {
                            new Header(DataAcquisitionConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId))
                        },
                        Value = new ResourceAcquired
                        {
                            Resource = resource,
                            ScheduledReports = scheduledReports,
                            PatientId = RemovePatientId(e.Resource) ? string.Empty : patientId,
                            QueryType = queryType
                        }
                    });
            }
        });
    }

    private async Task ProcessIQueryList(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlan queryPlan,
        string queryPlanType
    )
    {
        List<ResourceReference> referenceResources = new List<ResourceReference>();
        foreach (var query in queryList)
        {
            var queryConfig = query.Value;
            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig => ParameterQueryFactory.Build((ParameterQueryConfig)queryConfig, request,
                    scheduledReport, queryPlan.LookBack),
                ReferenceQueryConfig => ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, referenceResources),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogInformation("Processing Query for:");

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var bundle = await _fhirRepo.GetSingularBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.ConsumeResult.Message.Value.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    (SingularParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle));

                await GenerateMessagesFromBundle(bundle, request.ConsumeResult.Message.Value.PatientId, queryPlanType, request.CorrelationId, new List<ScheduledReport> { scheduledReport }, CancellationToken.None);
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = (ParameterQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                var bundle = await _fhirRepo.GetPagedBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.ConsumeResult.Message.Value.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    (PagedParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);

                referenceResources.AddRange(ReferenceResourceBundleExtractor.Extract(bundle));

                await GenerateMessagesFromBundle(bundle, request.ConsumeResult.Message.Value.PatientId, queryPlanType, request.CorrelationId, new List<ScheduledReport> { scheduledReport }, CancellationToken.None);
            }

            if (builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                var referenceQueryFactoryResult = (ReferenceQueryFactoryResult)builtQuery;

                var queryInfo = (ReferenceQueryConfig)queryConfig;
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                await _referenceResourceService.Execute(
                    referenceQueryFactoryResult,
                    request,
                    fhirQueryConfiguration,
                    queryInfo,
                    queryPlanType);
            }

        }
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }

    private async Task<bool> ValidateRequest(GetPatientDataRequest request)
    {
        var requestString = JsonConvert.SerializeObject(request);

        if (request.FacilityId == null)
        {
            var message = $"Facility not provided.";
            _logger.LogWarning(message);
            return false;
        }

        if (request.ConsumeResult.Message.Value.ScheduledReports.Count == 0)
        {
            var message = $"Scheduled Reports not provided.";
            _logger.LogWarning(message);
            return false;
        }

#if DEBUG
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            request.CorrelationId = Guid.NewGuid().ToString();
        }
#endif

#if !DEBUG
            if(string.IsNullOrWhiteSpace(request.CorrelationId)) 
            {
                var message = $"Correlation Id not provided.";
                _logger.LogWarning(message);
                await GenerateAuditMessage(message, request, "N/A", "No CorrelationId");
                return false;
            }
#endif


        if (request.ConsumeResult.Message.Value.PatientId == null)
        {
            var message = $"Patient Id not provided.";
            _logger.LogWarning(message);
            return false;
        }

        return true;
    }
}
