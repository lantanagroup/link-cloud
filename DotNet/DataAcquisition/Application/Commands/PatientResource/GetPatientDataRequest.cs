using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Commands.Audit;
using LantanaGroup.Link.DataAcquisition.Application.Factories.QueryFactories;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ReferenceQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Settings;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using MediatR;
using Newtonsoft.Json;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Application.Commands.PatientResource;

public class GetPatientDataRequest : IRequest<List<IBaseMessage>>
{
    public string FacilityId { get; set; }
    public DataAcquisitionRequestedMessage Message { get; set; }
    public string CorrelationId { get; set; }
    public QueryPlanType QueryPlanType { get; set; }
}

public class GetPatientDataRequestHandler : IRequestHandler<GetPatientDataRequest, List<IBaseMessage>>
{
    private readonly ILogger<GetPatientDataRequestHandler> _logger;
    private readonly IFhirQueryConfigurationRepository _fhirQueryRepo;
    private readonly IQueryPlanRepository _queryPlanRepository;
    private readonly IReferenceResourcesRepository _referenceResourcesRepository;
    private readonly IMediator _mediator;
    private readonly IFhirApiRepository _fhirRepo;

    public GetPatientDataRequestHandler(
        ILogger<GetPatientDataRequestHandler> logger,
        IFhirQueryConfigurationRepository fhirQueryRepo,
        IQueryPlanRepository queryPlanRepository,
        IMediator mediator,
        IFhirApiRepository fhirRepo,
        IReferenceResourcesRepository referenceResourcesRepository,
        IQueriedFhirResourceRepository queriedFhirResourceRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fhirQueryRepo = fhirQueryRepo ?? throw new ArgumentNullException(nameof(fhirQueryRepo));
        _queryPlanRepository = queryPlanRepository ?? throw new ArgumentNullException(nameof(queryPlanRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _fhirRepo = fhirRepo ?? throw new ArgumentNullException(nameof(fhirRepo));
        _referenceResourcesRepository = referenceResourcesRepository ?? throw new ArgumentNullException(nameof(referenceResourcesRepository));
    }

    public async Task<List<IBaseMessage>> Handle(GetPatientDataRequest request, CancellationToken cancellationToken)
    {
        List<IBaseMessage> messages = new List<IBaseMessage>();

        if(!(await ValidateRequest(request)))
        {
            return null;
        }

        FhirQueryConfiguration fhirQueryConfiguration = null;
        List<QueryPlan> queryPlans = null;

        try
        {
            fhirQueryConfiguration = await _fhirQueryRepo.GetAsync(request.FacilityId, cancellationToken);
            queryPlans = await _queryPlanRepository.GetQueryPlansByFacilityId(request.FacilityId, cancellationToken);

            if(fhirQueryConfiguration == null || queryPlans == null) 
            {
                throw new Exception($"No configuration for {request.FacilityId} exists.");
            }
        }
        catch(Exception ex)
        {
            var message = $"Error retrieving configuration for facility {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
            _logger.LogError(message);
            await GenerateAuditMessage(message, request, "N/A");
            return null;
        }

        foreach(var scheduledReport in request.Message.ScheduledReports)
        {
            var patientId = TEMPORARYPatientIdPart(request.Message.PatientId);
            Bundle bundle = new Bundle();
            bundle.Type = Bundle.BundleType.Transaction;
            bundle.Identifier = new Identifier 
            {
                Value = patientId
            };

            Patient patient = null;

            if (request.Message.QueryType.Equals("Initial", StringComparison.InvariantCultureIgnoreCase))
            {
                patient = await _fhirRepo.GetPatient(
                fhirQueryConfiguration.FhirServerBaseUrl,
                patientId, request.CorrelationId,
                request.FacilityId,
                fhirQueryConfiguration.Authentication);

                bundle.AddResourceEntry(patient, patientId);
            }

            var queryPlan = queryPlans.Where(x => x.ReportType == scheduledReport.ReportType).FirstOrDefault();
            if(queryPlan != null)
            {
                var initialQueries = queryPlan.InitialQueries.OrderBy(x => x.Key);
                var supplementalQueries = queryPlan.SupplementalQueries.OrderBy(x => x.Key);
                (string queryPlanType, Bundle bundle) processedBundle = (null, bundle);
                try
                {
                    if (request.Message.QueryType == "Initial")
                    {
                        processedBundle = await ProcessIQueryList(initialQueries, request, fhirQueryConfiguration, scheduledReport, queryPlan, processedBundle.bundle, QueryPlanType.InitialQueries.ToString());
                    }
                    else
                    {
                        processedBundle = await ProcessIQueryList(supplementalQueries, request, fhirQueryConfiguration, scheduledReport, queryPlan, processedBundle.bundle, QueryPlanType.SupplementalQueries.ToString());
                    }

                    foreach (var entry in processedBundle.bundle.Entry)
                    {

                        foreach (var child in entry.Resource.Children)
                        {
                            if (child is Resource)
                            {
                                var childResource = (Resource)child;
                                messages.Add(new ResourceAcquired
                                {
                                    Resource = childResource,
                                    ScheduledReports = new List<ScheduledReport> { scheduledReport },
                                    PatientId = patientId,
                                    QueryType = request.Message.QueryType
                                });
 
                            }
                        }

                        messages.Add(new ResourceAcquired
                        {
                            Resource = entry.Resource,
                            ScheduledReports = new List<ScheduledReport> { scheduledReport },
                            PatientId = RemovePatientId(entry.Resource) ? string.Empty : patientId,
                            QueryType = request.Message.QueryType
                        });
                    }

                }
                catch(Exception ex)
                {
                    var message = $"Error retrieving data from EHR for facility: {request.FacilityId}\n{ex.Message}\n{ex.InnerException}";
                    _logger.LogError(message);
                    await GenerateAuditMessage(message, request, scheduledReport.ReportType);
                    return null;
                }
            }  
        }

        return messages;
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

    private async Task<(string queryPlanType, Bundle bundle)> ProcessIQueryList(
        IOrderedEnumerable<KeyValuePair<string, IQueryConfig>> queryList,
        GetPatientDataRequest request,
        FhirQueryConfiguration fhirQueryConfiguration,
        ScheduledReport scheduledReport,
        QueryPlan queryPlan,
        Bundle bundle,
        string queryPlanType
        )
    {
        foreach (var query in queryList)
        {

            var queryConfig = query.Value;
            QueryFactoryResult builtQuery = queryConfig switch
            {
                ParameterQueryConfig => ParameterQueryFactory.Build((ParameterQueryConfig)queryConfig, request, scheduledReport, queryPlan.LookBack, bundle),
                ReferenceQueryConfig => ReferenceQueryFactory.Build((ReferenceQueryConfig)queryConfig, bundle),
                _ => throw new Exception("Unable to identify type for query operation."),
            };

            _logger.LogInformation("Processing Query for:");

            if (builtQuery.GetType() == typeof(SingularParameterQueryFactoryResult))
            {
                var queryInfo = ((ParameterQueryConfig)queryConfig);
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                bundle = await _fhirRepo.GetSingularBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.Message.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    bundle,
                    (SingularParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);
            }

            if (builtQuery.GetType() == typeof(PagedParameterQueryFactoryResult))
            {
                var queryInfo = ((ParameterQueryConfig)queryConfig);
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                bundle = await _fhirRepo.GetPagedBundledResultsAsync(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    request.Message.PatientId,
                    request.CorrelationId,
                    request.FacilityId,
                    queryPlanType,
                    bundle,
                    (PagedParameterQueryFactoryResult)builtQuery,
                    (ParameterQueryConfig)queryConfig,
                    scheduledReport,
                    fhirQueryConfiguration.Authentication);
            }

            if(builtQuery.GetType() == typeof(ReferenceQueryFactoryResult))
            {
                var referenceQueryFactoryResult = (ReferenceQueryFactoryResult)builtQuery;

                var queryInfo = ((ReferenceQueryConfig)queryConfig);
                _logger.LogInformation("Resource: {1}", queryInfo.ResourceType);

                if (referenceQueryFactoryResult.ReferenceIds?.Count == 0)
                {
                    break;
                }

                var existingReferenceResources = await _referenceResourcesRepository.GetReferenceResourcesForListOfIds(referenceQueryFactoryResult.ReferenceIds.Select(x => x.ElementId).ToList(), request.FacilityId);

                bundle = AddExisitngReferenceListToBundle(existingReferenceResources, bundle);

                List<ResourceReference> missingReferences = referenceQueryFactoryResult.ReferenceIds.Where(x => !existingReferenceResources.Any(y => y.ResourceId == x.ElementId)).ToList();
                var fullMissingResources = await _fhirRepo.GetReferenceResource(
                    fhirQueryConfiguration.FhirServerBaseUrl,
                    referenceQueryFactoryResult.ResourceType,
                    request.Message.PatientId,
                    request.FacilityId,
                    request.CorrelationId,
                    queryPlanType,
                    missingReferences,
                    (ReferenceQueryConfig)queryConfig,
                    fhirQueryConfiguration.Authentication);

                var fullRefResourceList = new List<ReferenceResources>();
                fullRefResourceList.AddRange(existingReferenceResources);

                var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                var currentDateTime = System.DateTime.UtcNow;
                var fhirDateTime = new FhirDateTime(
                                    currentDateTime.Year,
                                    currentDateTime.Month,
                                    currentDateTime.Day,
                                    currentDateTime.Hour,
                                    currentDateTime.Minute,
                                    currentDateTime.Second,
                                    TimeSpan.Zero);

                foreach (var resource in fullMissingResources)
                {
                    if (resource.TypeName == nameof(OperationOutcome))
                    {
                        var opOutcome = (OperationOutcome)resource;
                        var message = $"Operation Outcome encountered:\n {opOutcome.Text}";
                        _logger.LogWarning(message);
                        await GenerateAuditMessage(message, request, referenceQueryFactoryResult.ResourceType, request.FacilityId);
                        continue;
                    }

                    var refResource = new ReferenceResources
                    {
                        FacilityId = request.FacilityId,
                        ResourceId = resource.Id,
                        ReferenceResource =  System.Text.Json.JsonSerializer.Serialize(resource, jsonOptions),
                        ResourceType = referenceQueryFactoryResult.ResourceType,
                        CreateDate = currentDateTime,
                        ModifyDate = currentDateTime,
                    }; 
                    _referenceResourcesRepository.Add(refResource);
                    bundle.AddResourceEntry(resource, $"{resource.TypeName}/{resource.Id}");

                }
            }

        }
        return (queryPlanType, bundle);
    }

    private Bundle AddExisitngReferenceListToBundle(List<ReferenceResources> resources, Bundle bundle)
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

        foreach (var resource in resources)
        {
            object? _ = resource.ResourceType switch
            {
                nameof(Condition) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Condition>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Coverage) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Coverage>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Encounter) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Encounter>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Location) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Location>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Medication) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Medication>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(MedicationRequest) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<MedicationRequest>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Observation) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Observation>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Patient) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Patient>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Procedure) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Procedure>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(ServiceRequest) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<ServiceRequest>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                nameof(Specimen) => Do(() => {
                    var r = System.Text.Json.JsonSerializer.Deserialize<Specimen>(resource.ReferenceResource, options);
                    bundle.AddResourceEntry(r, r.ResourceBase.AbsoluteUri);
                }),
                _ => throw new Exception($"Resource Type {resource.ResourceType} not configured for Read operation."),
            };
        }
        return bundle;
    }

    public static object Do(Action action)
    {
        action();
        return new object();
    }

    private async System.Threading.Tasks.Task GenerateAuditMessage(
        string message, 
        GetPatientDataRequest request,
        string resource,
        string key = null
        )
    {
        string theKey = null;
        if (string.IsNullOrWhiteSpace(request?.FacilityId))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                theKey = "N/A";
            }
            else
            {
                theKey = key;
            }
        }
        else
        {
            theKey = request.FacilityId;
        }

        await _mediator.Send(new TriggerAuditEventCommand
        {
            AuditableEvent = new AuditEventMessage
            {
                CorrelationId = string.IsNullOrWhiteSpace(request?.CorrelationId) ? "NoCorrelationId" : request.CorrelationId,
                FacilityId = theKey,
                Action = AuditEventType.Query,
                EventDate = DateTime.UtcNow,
                ServiceName = DataAcquisitionConstants.ServiceName,
                Resource = resource,
                Notes = message
            }
        });
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
            await GenerateAuditMessage(message, request, "N/A", "Unknown Facility Id");
            return false;
        }

        if (request.Message.ScheduledReports.Count == 0) 
        {
            var message = $"Scheduled Reports not provided.";
            _logger.LogWarning(message);
            await GenerateAuditMessage(message, request, "N/A", "No Scheduled Reports");
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


        if (request.Message.PatientId == null) 
        {
            var message = $"Patient Id not provided.";
            _logger.LogWarning(message);
            await GenerateAuditMessage(message, request, "N/A", "No PatientId");
            return false;
        }

        return true;
    }
}
