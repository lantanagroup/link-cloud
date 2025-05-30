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

namespace LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi;

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
    private readonly IFhirQueryManager _fhirQueryManager;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly BundleResourceAcquiredEventService _bundleResourceAcquiredEventService;
    private readonly IReferenceResourcesManager _referenceResourceManager;

    public FhirApiService(
        ILogger<FhirApiService> logger,
        HttpClient httpClient,
        IAuthenticationRetrievalService authenticationRetrievalService,
        IFhirQueryManager fhirQueryManager,
        IDataAcquisitionServiceMetrics metrics,
        BundleResourceAcquiredEventService bundleResourceAcquiredEventService,
        IReferenceResourcesManager referenceResourceManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentException(nameof(authenticationRetrievalService));
        _fhirQueryManager = fhirQueryManager ?? throw new ArgumentNullException(nameof(fhirQueryManager));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _bundleResourceAcquiredEventService = bundleResourceAcquiredEventService ?? throw new ArgumentNullException(nameof(bundleResourceAcquiredEventService));
        _referenceResourceManager = referenceResourceManager ?? throw new ArgumentNullException(nameof(referenceResourceManager));
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

        if (query.opType == OperationType.Read)
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

        return (Patient)await ReadFhirEndpointAsync(fhirClient, nameof(Patient), patientId, patientId, correlationId, facilityId, QueryPlanType.Initial.ToString());
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

            var resultBundle = await fhirClient.SearchAsync(searchParams, resourceType, ct: cancellationToken);


            await _fhirQueryManager.AddAsync(new FhirQuery
            {
                ResourceType = resourceType,
                CorrelationId = correlationId,
                PatientId = patientId.SplitReference(),
                FacilityId = facilityId,
                SearchParams = JsonSerializer.Serialize(searchParams),
            }, cancellationToken);
            

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
                        var fhirDateTime = new FhirDateTime(
                            currentDateTime.Year,
                            currentDateTime.Month,
                            currentDateTime.Day,
                            currentDateTime.Hour,
                            currentDateTime.Minute,
                            currentDateTime.Second,
                            TimeSpan.Zero);

                        var refResource = new ReferenceResources
                        {
                            FacilityId = facilityId,
                            ResourceId = resource.Id,
                            ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, jsonOptions),
                            ResourceType = resourceType,
                            CreateDate = currentDateTime,
                            ModifyDate = currentDateTime,
                        };

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
                                var fhirDateTime = new FhirDateTime(
                                    currentDateTime.Year,
                                    currentDateTime.Month,
                                    currentDateTime.Day,
                                    currentDateTime.Hour,
                                    currentDateTime.Minute,
                                    currentDateTime.Second,
                                    TimeSpan.Zero);

                                var refResource = new ReferenceResources
                                {
                                    FacilityId = facilityId,
                                    ResourceId = resource.Id,
                                    ReferenceResource = System.Text.Json.JsonSerializer.Serialize(resource, jsonOptions),
                                    ResourceType = resourceType,
                                    CreateDate = currentDateTime,
                                    ModifyDate = currentDateTime,
                                };

                                await _referenceResourceManager.AddAsync(refResource);
                            }

                            IncrementResourceAcquiredMetric(correlationId, patientId, facilityId, queryType, resourceType, entry.Resource.Id);
                        }

                        if (referenceTypes != default)
                            references.AddRange(ReferenceResourceBundleExtractor.Extract(resultBundle, referenceTypes));
                    }
                }
            }

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

        try
        {
            string location = resourceType switch
            {
                nameof(List) => $"{fhirClient.Endpoint}/List/{id}",
                nameof(Patient) => TEMPORARYPatientIdPart(id),
                _ => id
            };
            readResource = await fhirClient.ReadAsync<DomainResource>(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; PatientId: {PatientId}", resourceType, patientId);
            throw;
        }

        await _fhirQueryManager.AddAsync(new Domain.Entities.FhirQuery
        {
            ResourceType = resourceType,
            CorrelationId = correlationId,
            PatientId = patientId.SplitReference(),
            FacilityId = facilityId,
        }, cancellationToken);

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

        if(config.OperationType == OperationType.Read)
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

        if (query.opType == OperationType.Read)
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

        if (config.OperationType == OperationType.Read)
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
            var fhirDateTime = new FhirDateTime(
                currentDateTime.Year,
                currentDateTime.Month,
                currentDateTime.Day,
                currentDateTime.Hour,
                currentDateTime.Minute,
                currentDateTime.Second,
                TimeSpan.Zero);

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
