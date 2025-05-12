using DataAcquisition.Domain.Entities;
using DataAcquisition.Domain.Models.Enums;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Net.Http.Headers;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using DAEnums = LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

public interface IFhirApiService
{
    Task<Bundle> GetPagedBundledResultsAsync(
        string baseUrl,
        string patientIdReference,
        string correlationId,
        string facilityId,
        string queryType,
        PagedParameterQueryFactoryResult pagedQuery,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig);

    Task<List<ResourceReference>> GetPagedBundledResultAndGenerateMessagesAsync(
        string baseUrl, 
        GetPatientDataRequest request, 
        string queryType, 
        List<string> referenceTypes, 
        PagedParameterQueryFactoryResult pagedQuery, 
        ParameterQueryConfig config, 
        AuthenticationConfiguration authConfig);

    Task<Bundle> GetSingularBundledResultsAsync(
        string baseUrl,
        string patientIdReference,
        string correlationId,
        string facilityId,
        string queryType,
        SingularParameterQueryFactoryResult query,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig);

    Task<List<ResourceReference>> GetSingularBundledResultsAndGenerateMessagesAsync(
        string baseUrl, 
        GetPatientDataRequest request,
        string queryType, 
        List<string> resourceTypes, 
        SingularParameterQueryFactoryResult query, 
        ParameterQueryConfig config, 
        AuthenticationConfiguration authConfig);

    Task<Patient> GetPatient(
        string baseUrl,
        string patientId,
        string correlationId,
        string facilityId,
        AuthenticationConfiguration authConfig,
        ScheduledReport report,
        CancellationToken cancellationToken = default);

    Task<List> GetPatientList(
        string baseUrl,
        string listId,
        string facilityId,
        AuthenticationConfiguration authConfig,
        CancellationToken cancellationToken = default);

    Task<List<DomainResource>> GetReferenceResource(
        string baseUrl,
        string resourceType,
        string patientIdReference,
        string facilityIdReference,
        string correlationId,
        string queryPlanType,
        ResourceReference referenceId,
        ReferenceQueryConfig config,
        AuthenticationConfiguration authConfig);

    Task GetReferenceResourceAndGenerateMessage(
        string baseUrl,
        string resourceType,
        GetPatientDataRequest request,
        string queryPlanType,
        ResourceReference referenceId,
        ReferenceQueryConfig config,
        AuthenticationConfiguration authConfig);
}

public class FhirApiService : IFhirApiService
{
    private readonly ILogger<FhirApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationRetrievalService _authenticationRetrievalService;
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly BundleResourceAcquiredEventService _bundleResourceAcquiredEventService;
    private readonly IReferenceResourcesManager _referenceResourceManager;

    public FhirApiService(
        ILogger<FhirApiService> logger,
        HttpClient httpClient,
        IAuthenticationRetrievalService authenticationRetrievalService,
        IDataAcquisitionServiceMetrics metrics,
        BundleResourceAcquiredEventService bundleResourceAcquiredEventService,
        IReferenceResourcesManager referenceResourceManager,
        IDataAcquisitionLogManager dataAcquisitionLogManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentException(nameof(authenticationRetrievalService));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _bundleResourceAcquiredEventService = bundleResourceAcquiredEventService ?? throw new ArgumentNullException(nameof(bundleResourceAcquiredEventService));
        _referenceResourceManager = referenceResourceManager ?? throw new ArgumentNullException(nameof(referenceResourceManager));
        _dataAcquisitionLogManager = dataAcquisitionLogManager ?? throw new ArgumentNullException(nameof(dataAcquisitionLogManager));
    }

    public async Task<Bundle> GetPagedBundledResultsAsync(
        string baseUrl,
        string patientIdReference,
        string correlationId,
        string facilityId,
        string queryType,
        PagedParameterQueryFactoryResult pagedQuery,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig)
    {
        var bundle = new Bundle();
        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(facilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        if (pagedQuery?.SearchParamsList == null)
        {
            throw new Exception("SearchParamList is null. Unable to Search fhir endpoint.");
        }

        foreach (var parameters in pagedQuery.SearchParamsList)
        {
            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                parameters.Add(kvPair.Key, kvPair.Value);
            }

            var resultBundle = await SearchFhirEndpointAsync(parameters, fhirClient, config.ResourceType);
            foreach (var b in resultBundle.bundle.Entry)
            {
                bundle.AddResourceEntry(b.Resource, b.FullUrl);
            }
        }
        return bundle;
    }

    public async Task<Bundle> GetSingularBundledResultsAsync(
        string baseUrl,
        string patientIdReference,
        string correlationId,
        string facilityId,
        string queryType,
        SingularParameterQueryFactoryResult query,
        ParameterQueryConfig config,
        ScheduledReport report,
        AuthenticationConfiguration authConfig)
    {
        var bundle = new Bundle();

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(facilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        if (query.opType == Domain.Models.QueryConfig.OperationType.Read)
        {
            if (query?.ResourceId == null)
            {
                throw new Exception("Resource ID is null. Unable to Read fhir endpoint.");
            }

            var resourceId = query.ResourceId;

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                if (resourceId.Contains("?"))
                {
                    resourceId = $"{resourceId}&{kvPair.Key}={kvPair.Value}";
                }
                else
                {
                    resourceId = $"{resourceId}?{kvPair.Key}={kvPair.Value}";
                }
            }

            var resource = await ReadFhirEndpointAsync(fhirClient, config.ResourceType, resourceId, patientIdReference, correlationId, facilityId, queryType);
            bundle.AddResourceEntry(resource, resource.ResourceBase.AbsolutePath);
        }
        else
        {
            if (query?.SearchParams == null)
            {
                throw new Exception("SearchParams is null. Unable to Search fhir endpoint.");
            }

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                query.SearchParams.Add(kvPair.Key, kvPair.Value);
            }

            var resourceBundle = await SearchFhirEndpointAsync(query.SearchParams, fhirClient, config.ResourceType, patientIdReference, correlationId, facilityId, queryType);
            resourceBundle.bundle.Entry.ForEach(x =>
            {
                if (!(x.Resource.TypeName == nameof(OperationOutcome)))
                {
                    bundle.AddResourceEntry(x.Resource, x.FullUrl);                    
                }
            });
        }

        return bundle;
    }

    public async Task<Patient> GetPatient(
        string baseUrl,
        string patientId,
        string correlationId,
        string facilityId,
        AuthenticationConfiguration authConfig,
        ScheduledReport report,
        CancellationToken cancellationToken = default)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, "Patient"),
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, QueryPlanType.Initial.ToString())
        ]);

        patientId = patientId.Contains("Patient/", StringComparison.InvariantCultureIgnoreCase) ? patientId : $"Patient/{patientId}";

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(facilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        return (Patient)await ReadFhirEndpointAsync(fhirClient, nameof(Patient), patientId, patientId, correlationId, facilityId, QueryPlanType.Initial.ToString(), report: report);
    }

    public async Task<List> GetPatientList(string baseUrl, string listId, string facilityId, AuthenticationConfiguration authConfig, CancellationToken cancellationToken = default)
    {
        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(facilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        return (List)await ReadFhirEndpointAsync(fhirClient, nameof(List), listId, facilityId: facilityId);
    }

    private async Task<(Bundle bundle, List<ResourceReference> ResourceReference)> SearchFhirEndpointAsync(
        SearchParams searchParams,
        FhirClient fhirClient,
        string resourceType,
        string? patientId = default,
        string? correlationId = default,
        string? facilityId = default,
        string? queryType = default,
        List<ScheduledReport>? reports = default,
        List<string>? referenceTypes = default,
        ReportableEvent reportableEvent = default,
        bool generateMessages = false,
        bool returnBundle = true,
        bool saveReferenceResource = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType)
            ]);

            List<ResourceReference> references = new List<ResourceReference>();

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            
            var log = await _dataAcquisitionLogManager.CreateAsync(new DataAcquisitionLog
            {
                FacilityId = facilityId,
                Priority = AcquisitionPriority.Normal,
                PatientId = patientId,
                FhirVersion = "R4",
                QueryType = FhirQueryType.Search,
                QueryPhase = queryType.TranslateToQueryPhase(),
                FhirQuery = new List<FhirQuery>
                {
                    new FhirQuery
                    {
                        Id = Guid.NewGuid().ToString(),
                        FacilityId = facilityId,
                        CreateDate = DateTime.UtcNow,
                        ModifyDate = DateTime.UtcNow,
                        QueryType = FhirQueryType.Search,
                        QueryParameters = searchParams.ToUriParamList().Select(x => $"{x.Item1}={x.Item2}").ToList(),
                        ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> { ResourceTypeModelUtilities.ToDomain(resourceType) },
                        ResourceReferenceTypes = referenceTypes?.ConvertAll(x => new ResourceReferenceType
                        {
                            Id = Guid.NewGuid().ToString(),
                            FacilityId = facilityId,
                            CreateDate = DateTime.UtcNow,
                            ModifyDate = DateTime.UtcNow,
                            QueryPhase = queryType.TranslateToQueryPhase(),
                            ResourceType = x,
                        }),
                    }
                },
                Status = DAEnums.RequestStatus.Processing,
                ExecutionDate = DateTime.UtcNow,
                TimeZone = TimeZoneInfo.Utc.DisplayName,
                RetryAttempts = 0,
                CompletionDate = null,
                CompletionTimeMilliseconds = null,
                ResourceAcquiredIds = new List<string>(),
                ScheduledReport = reports?[0],
                CorrelationId = correlationId,
            }, cancellationToken);

            Bundle? resultBundle = null;
            try
            {
                resultBundle = await fhirClient.SearchAsync(searchParams, resourceType, ct: cancellationToken);
            }
            catch (Exception ex)
            {
                stopWatch.Stop();
                log.CompletionDate = DateTime.UtcNow;
                log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
                log.Status = DAEnums.RequestStatus.Failed;
                log.ResourceAcquiredIds = new List<string>();
                await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

                throw;
            }

            if (resultBundle != null)
            {
                if (generateMessages)
                    await _bundleResourceAcquiredEventService.GenerateEventAsync(resultBundle, new ResourceAcquiredMessageGenerationRequest(facilityId, patientId, queryType, correlationId, reportableEvent, reports), cancellationToken);

                foreach (var entry in resultBundle.Entry)
                {
                    if (saveReferenceResource) 
                    {
                        var resource = entry.Resource;
                        if (resource.TypeName == nameof(OperationOutcome))
                        {
                            var opOutcome = (OperationOutcome)resource;
                            _logger.LogWarning("Operation Outcome encountered:\n {opOutcome}", opOutcome.Text);
                            continue;
                        }

                        var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                        var currentDateTime = DateTime.UtcNow;

                        var refResource = new ReferenceResources
                        {
                            FacilityId = facilityId,
                            ResourceId = resource.Id,
                            ReferenceResource = JsonSerializer.Serialize(resource, jsonOptions),
                            ResourceType = resourceType,
                            CreateDate = currentDateTime,
                            ModifyDate = currentDateTime,
                        };

                        log.ReferenceResources.Add(refResource);

                        await _referenceResourceManager.AddAsync(refResource);
                    }

                    IncrementResourceAcquiredMetric(correlationId, patientId, facilityId, queryType, resourceType, entry.Resource.Id);
                }

                if (referenceTypes != default)
                    references.AddRange(ReferenceResourceBundleExtractor.Extract(resultBundle, referenceTypes));
            }

            Bundle? newResultBundle = resultBundle;

            if (newResultBundle != null)
            {
                while (resultBundle.Link.Exists(x => x.Relation == "next"))
                {
                    resultBundle = await fhirClient.ContinueAsync(resultBundle, ct: cancellationToken);

                    if (resultBundle != null && resultBundle.Entry.Any())
                    {
                        if (returnBundle)
                            newResultBundle.Entry.AddRange(resultBundle.Entry);
                        
                        if(generateMessages)
                            await _bundleResourceAcquiredEventService.GenerateEventAsync(resultBundle, new ResourceAcquiredMessageGenerationRequest(facilityId, patientId, queryType, correlationId, reportableEvent, reports), cancellationToken);

                        foreach (var entry in resultBundle.Entry)
                        {
                            if (saveReferenceResource)
                            {
                                var resource = entry.Resource;
                                if (resource.TypeName == nameof(OperationOutcome))
                                {
                                    var opOutcome = (OperationOutcome)resource;
                                    _logger.LogWarning("Operation Outcome encountered:\n {opOutcome}", opOutcome.Text);
                                    continue;
                                }

                                var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                                var currentDateTime = DateTime.UtcNow;

                                var refResource = new ReferenceResources
                                {
                                    FacilityId = facilityId,
                                    ResourceId = resource.Id,
                                    ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, jsonOptions),
                                    ResourceType = resourceType,
                                    CreateDate = currentDateTime,
                                    ModifyDate = currentDateTime,
                                };

                                log.ReferenceResources.Add(refResource);

                                await _referenceResourceManager.AddAsync(refResource);
                            }

                            IncrementResourceAcquiredMetric(correlationId, patientId, facilityId, queryType, resourceType, entry.Resource.Id);
                        }

                        if (referenceTypes != default)
                            references.AddRange(ReferenceResourceBundleExtractor.Extract(resultBundle, referenceTypes));
                    }
                }
            }
            
            stopWatch.Stop();
            log.CompletionDate = DateTime.UtcNow;
            log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
            log.Status = DAEnums.RequestStatus.Completed;
            log.ResourceAcquiredIds = resultBundle.Entry.Select(x => x.Resource.Id).ToList();

            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

            return (newResultBundle, references);
        }
        catch (FhirOperationException ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    private async Task<DomainResource> ReadFhirEndpointAsync(
        FhirClient fhirClient,
        string resourceType,
        string id,
        string? patientId = default,
        string? correlationId = default,
        string? facilityId = default,
        string? queryType = default,
        ReportableEvent reportableEvent = default,
        ScheduledReport? report = default,
        bool generateMessages = false,
        CancellationToken cancellationToken = default)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType),
            new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, id)
        ]);

        DomainResource? readResource = null;

        var log = await _dataAcquisitionLogManager.CreateAsync(new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Priority = AcquisitionPriority.Normal,
            PatientId = patientId,
            FhirVersion = "R4",
            QueryType = FhirQueryType.Search,
            QueryPhase = queryType.TranslateToQueryPhase(),
            FhirQuery = new List<FhirQuery>(),
            Status = DAEnums.RequestStatus.Processing,
            ExecutionDate = DateTime.UtcNow,
            TimeZone = TimeZoneInfo.Utc.DisplayName,
            RetryAttempts = 0,
            CompletionDate = null,
            CompletionTimeMilliseconds = null,
            ResourceAcquiredIds = new List<string>(),
            ScheduledReport = report,
            CorrelationId = correlationId,
        }, cancellationToken);

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        try
        {
            readResource = resourceType switch
            {
                nameof(Condition) => await fhirClient.ReadAsync<Condition>(id),
                nameof(Coverage) => await fhirClient.ReadAsync<Coverage>(id),
                nameof(Encounter) => await fhirClient.ReadAsync<Encounter>(id),
                nameof(Location) => await fhirClient.ReadAsync<Location>(id),
                nameof(Medication) => await fhirClient.ReadAsync<Medication>(id),
                nameof(MedicationRequest) => await fhirClient.ReadAsync<MedicationRequest>(id),
                nameof(Observation) => await fhirClient.ReadAsync<Observation>(id),
                nameof(Patient) => await fhirClient.ReadAsync<Patient>(TEMPORARYPatientIdPart(id)),
                nameof(Procedure) => await fhirClient.ReadAsync<Procedure>(id),
                nameof(ServiceRequest) => await fhirClient.ReadAsync<ServiceRequest>(id),
                nameof(Specimen) => await fhirClient.ReadAsync<Specimen>(id),
                nameof(List) => await fhirClient.ReadAsync<List>($"{fhirClient.Endpoint}/List/{id}"),
                _ => throw new Exception($"Resource Type {resourceType} not configured for Read operation."),
            };
        }
        catch (Exception ex)
        {
            stopWatch.Stop();
            log.CompletionDate = DateTime.UtcNow;
            log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
            log.Status = DAEnums.RequestStatus.Failed;
            log.ResourceAcquiredIds = new List<string>();
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

            _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; PatientId: {PatientId}", resourceType, patientId);
            throw;
        }

        stopWatch.Stop();
        log.CompletionDate = DateTime.UtcNow;
        log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
        log.Status = DAEnums.RequestStatus.Completed;
        log.ResourceAcquiredIds = new List<string> { readResource?.Id };
        await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

        if (readResource != null)
        {
            if (generateMessages)
                await _bundleResourceAcquiredEventService.GenerateEventAsync(new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = readResource } } }, new ResourceAcquiredMessageGenerationRequest(facilityId, patientId, queryType, correlationId, reportableEvent, new List<ScheduledReport> { report }), cancellationToken);

            if (readResource is not OperationOutcome)
            {
                IncrementResourceAcquiredMetric(correlationId, patientId, facilityId, queryType, resourceType, id);
            }
        }


        return readResource;
    }

    public async Task<List<DomainResource>> GetReferenceResource(
        string baseUrl,
        string resourceType,
        string patientIdReference,
        string facilityIdReference,
        string correlationId,
        string queryPlanType,
        ResourceReference referenceId,
        ReferenceQueryConfig config,
        AuthenticationConfiguration authConfig)
    {
        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(facilityIdReference, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        List<DomainResource> domainResources = new List<DomainResource>();

        if(config.OperationType == Domain.Models.QueryConfig.OperationType.Read)
        {
            var refIdResult = GetRefId(referenceId, resourceType);

            if (!refIdResult.success)
                return domainResources;

            var refId = refIdResult.refId;

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                if (refId.Contains("?"))
                {
                    refId = $"{refId}&{kvPair.Key}={kvPair.Value}";
                }
                else
                {
                    refId = $"{refId}?{kvPair.Key}={kvPair.Value}";
                }
            }

            var result = await ReadFhirEndpointAsync(fhirClient, resourceType, refId, patientIdReference, correlationId, facilityIdReference, queryPlanType);
            domainResources.Add(result);
        }
        else
        {
            SearchParams searchParams = new SearchParams();
            try
            {
                var id = (string.IsNullOrWhiteSpace(referenceId.ElementId) ? referenceId.Url.ToString() : referenceId.ElementId).Split("/").LastOrDefault();
                if (string.IsNullOrWhiteSpace(id))
                    return domainResources;
                searchParams.Add("_id", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No appropriate ID found for reference.\n{referenceId.ToJson()}");
                return domainResources;
            }

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                searchParams.Add(kvPair.Key, kvPair.Value);
            }

            var result = await SearchFhirEndpointAsync(searchParams, fhirClient, resourceType, correlationId: correlationId, facilityId: facilityIdReference, queryType: queryPlanType);
            if (result.bundle != null)
            {
                domainResources.AddRange(result.bundle.Entry.Where(x => x.Resource is DomainResource && x.Resource.TypeName != nameof(OperationOutcome)).Select(x => (DomainResource)x.Resource).ToList());
            }
        }

        return domainResources;
    }

    public async Task<List<ResourceReference>> GetPagedBundledResultAndGenerateMessagesAsync(
        string baseUrl, 
        GetPatientDataRequest request,
        string queryType, 
        List<string> referenceTypes, 
        PagedParameterQueryFactoryResult pagedQuery, 
        ParameterQueryConfig config, 
        AuthenticationConfiguration authConfig)
    {
        List<ResourceReference> references = new List<ResourceReference>();

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(request.FacilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        if (pagedQuery?.SearchParamsList == null)
        {
            throw new Exception("SearchParamList is null. Unable to Search fhir endpoint.");
        }

        foreach (var parameters in pagedQuery.SearchParamsList)
        {
            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                parameters.Add(kvPair.Key, kvPair.Value);
            }

            var results = await SearchFhirEndpointAsync(parameters, fhirClient, config.ResourceType, request.ConsumeResult.Value.PatientId, request.CorrelationId, request.FacilityId, queryType, request.ConsumeResult.Value.ScheduledReports, referenceTypes, request.ConsumeResult.Value.ReportableEvent, true, false);
            references.AddRange(results.ResourceReference);
        }

        return references;
    }

    public async Task<List<ResourceReference>> GetSingularBundledResultsAndGenerateMessagesAsync(
        string baseUrl, 
        GetPatientDataRequest request,
        string queryType, 
        List<string> resourceTypes, 
        SingularParameterQueryFactoryResult query, 
        ParameterQueryConfig config,
        AuthenticationConfiguration authConfig)
    {
        List<ResourceReference> references = new List<ResourceReference>();

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(request.FacilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        if (query.opType == Domain.Models.QueryConfig.OperationType.Read)
        {
            if (query?.ResourceId == null)
            {
                throw new Exception("Resource ID is null. Unable to Read fhir endpoint.");
            }

            var resourceId = query.ResourceId;

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                if (resourceId.Contains("?"))
                {
                    resourceId = $"{resourceId}&{kvPair.Key}={kvPair.Value}";
                }
                else
                {
                    resourceId = $"{resourceId}?{kvPair.Key}={kvPair.Value}";
                }
            }

            var resource = await ReadFhirEndpointAsync(fhirClient, config.ResourceType, resourceId, request.ConsumeResult.Value.PatientId, request.CorrelationId, request.FacilityId, queryType, request.ConsumeResult.Value.ReportableEvent);
            
            await _bundleResourceAcquiredEventService.GenerateEventAsync(new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = resource } } }, new ResourceAcquiredMessageGenerationRequest(request.FacilityId, request.ConsumeResult.Value.PatientId, queryType, request.CorrelationId, request.ConsumeResult.Value.ReportableEvent, request.ConsumeResult.Value.ScheduledReports));

            references.AddRange(ReferenceResourceBundleExtractor.Extract(new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = resource } } }, resourceTypes));
        }
        else
        {
            if (query?.SearchParams == null)
            {
                throw new Exception("SearchParams is null. Unable to Search fhir endpoint.");
            }

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                query.SearchParams.Add(kvPair.Key, kvPair.Value);
            }

            var result = await SearchFhirEndpointAsync(query.SearchParams, fhirClient, config.ResourceType, request.ConsumeResult.Value.PatientId, request.CorrelationId, request.FacilityId, queryType, request.ConsumeResult.Value.ScheduledReports, resourceTypes, request.ConsumeResult.Value.ReportableEvent, true, false);

            references.AddRange(result.ResourceReference);
        }

        return references;
    }

    public async Task GetReferenceResourceAndGenerateMessage(
        string baseUrl,
        string resourceType,
        GetPatientDataRequest request,
        string queryPlanType,
        ResourceReference referenceId, 
        ReferenceQueryConfig config,
        AuthenticationConfiguration authConfig)
    {
        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(request.FacilityId, _authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        if (config.OperationType == Domain.Models.QueryConfig.OperationType.Read)
        {
            var refIdResult = GetRefId(referenceId, resourceType);

            if (!refIdResult.success)
                return;

            var refId = refIdResult.refId;

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                if (refId.Contains("?"))
                {
                    refId = $"{refId}&{kvPair.Key}={kvPair.Value}";
                }
                else
                {
                    refId = $"{refId}?{kvPair.Key}={kvPair.Value}";
                }
            }

            var result = await ReadFhirEndpointAsync(fhirClient, resourceType, refId, request.ConsumeResult.Value.PatientId.SplitReference(), request.CorrelationId, request.FacilityId, queryPlanType);

            if (result.TypeName == nameof(OperationOutcome))
            {
                var opOutcome = (OperationOutcome)result;
                _logger.LogWarning("Operation Outcome encountered:\n {opOutcome}", opOutcome.Text);
                return;
            }

            var jsonOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            var currentDateTime = DateTime.UtcNow;

            var refResource = new ReferenceResources
            {
                FacilityId = request.FacilityId,
                ResourceId = result.Id,
                ReferenceResource = System.Text.Json.JsonSerializer.Serialize(result, jsonOptions),
                ResourceType = resourceType,
                CreateDate = currentDateTime,
                ModifyDate = currentDateTime,
            };
            await _referenceResourceManager.AddAsync(refResource);

            await _bundleResourceAcquiredEventService.GenerateEventAsync(
                new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = result } } }, 
                new ResourceAcquiredMessageGenerationRequest(request.FacilityId, request.ConsumeResult.Value.PatientId?.SplitReference(), queryPlanType, request.CorrelationId, request.ConsumeResult.Value.ReportableEvent, request.ConsumeResult.Value.ScheduledReports));
        }
        else
        {
            SearchParams searchParams = new SearchParams();
            try
            {
                var id = (string.IsNullOrWhiteSpace(referenceId.ElementId) ? referenceId.Url.ToString() : referenceId.ElementId).Split("/").LastOrDefault();
                if (string.IsNullOrWhiteSpace(id))
                    return;
                searchParams.Add("_id", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No appropriate ID found for reference.\n{referenceId.ToJson()}");
                return;
            }

            if (authBuilderResults.isQueryParam)
            {
                var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                searchParams.Add(kvPair.Key, kvPair.Value);
            }

            await SearchFhirEndpointAsync(searchParams, fhirClient, resourceType, request.ConsumeResult.Value.PatientId?.SplitReference(), request.CorrelationId, request.FacilityId, queryPlanType, request.ConsumeResult.Value.ScheduledReports, null, request.ConsumeResult.Value.ReportableEvent, true, false, true);
        }
    }

    #region Private Methods
    private FhirClient GenerateFhirClient(string baseUrl)
    {
        return new FhirClient(baseUrl, _httpClient, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        });
    }

    private (bool success, string? refId) GetRefId(ResourceReference reference, string resourceType)
    {
        return resourceType switch
        {
            nameof(Location) => string.IsNullOrWhiteSpace(reference.Url?.ToString()) ? (false, null) : (true, reference.Url.ToString()),
            _ => string.IsNullOrWhiteSpace(reference.Url.ToString()) ? (false, null) : (true, reference.Url.ToString()),
        };
    }

    private void IncrementResourceAcquiredMetric(string? correlationId, string? patientIdReference, string? facilityId, string? queryType, string resourceType, string resourceId)
    {
        _metrics.IncrementResourceAcquiredCounter([
            new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, correlationId),
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientIdReference), //TODO: Can we keep this?
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType),
            new KeyValuePair<string, object?>(DiagnosticNames.ResourceId, resourceId)
        ]);
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
    }
    #endregion
}
