using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Application.Repositories;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Net.Http.Headers;
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
        string patientIdReference, 
        string correlationId, 
        string facilityId, 
        string queryType, 
        List<string> referenceTypes, 
        PagedParameterQueryFactoryResult pagedQuery, 
        ParameterQueryConfig config, 
        ScheduledReport report, 
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
        string patientIdReference, 
        string correlationId, 
        string facilityId, 
        string queryType, 
        List<string> resourceTypes, 
        SingularParameterQueryFactoryResult query, 
        ParameterQueryConfig config, 
        ScheduledReport report, 
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
}

public class FhirApiService : IFhirApiService
{
    private readonly ILogger<FhirApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationRetrievalService _authenticationRetrievalService;
    private readonly IQueriedFhirResourceManager _queriedFhirResourceManager;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly BundleResourceAcquiredEventService _bundleResourceAcquiredEventService;

    public FhirApiService(ILogger<FhirApiService> logger, HttpClient httpClient, IAuthenticationRetrievalService authenticationRetrievalService, IQueriedFhirResourceManager queriedFhirResourceManager, IDataAcquisitionServiceMetrics metrics, BundleResourceAcquiredEventService bundleResourceAcquiredEventService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentException(nameof(authenticationRetrievalService));
        _queriedFhirResourceManager = queriedFhirResourceManager ?? throw new ArgumentNullException(nameof(queriedFhirResourceManager));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _bundleResourceAcquiredEventService = bundleResourceAcquiredEventService ?? throw new ArgumentNullException(nameof(bundleResourceAcquiredEventService));
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

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
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

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
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

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        return (Patient)await ReadFhirEndpointAsync(fhirClient, nameof(Patient), patientId, patientId, correlationId, facilityId, QueryPlanType.Initial.ToString());
    }

    public async Task<List> GetPatientList(string baseUrl, string listId, AuthenticationConfiguration authConfig, CancellationToken cancellationToken = default)
    {
        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        return (List)await ReadFhirEndpointAsync(fhirClient, nameof(List), listId);
    }

    private async Task<(Bundle bundle, List<ResourceReference> ResourceReference)> SearchFhirEndpointAsync(
        SearchParams searchParams,
        FhirClient fhirClient,
        string resourceType,
        string? patientId = default,
        string? correlationId = default,
        string? facilityId = default,
        string? queryType = default,
        ScheduledReport? report = default,
        List<string>? referenceTypes = default,
        bool generateMessages = false,
        bool returnBundle = true,
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

            if (resultBundle != null)
            {
                if (generateMessages)
                    await _bundleResourceAcquiredEventService.GenerateEventAsync(resultBundle, new ResourceRequiredMessageRequest(facilityId, patientId, queryType, correlationId, new List<ScheduledReport> { report }), cancellationToken);

                foreach (var entry in resultBundle.Entry)
                {
                    await _queriedFhirResourceManager.AddAsync(new Domain.Entities.QueriedFhirResourceRecord
                    {
                        ResourceId = entry.Resource.Id,
                        ResourceType = entry.Resource.TypeName,
                        CorrelationId = correlationId,
                        PatientId = patientId,
                        FacilityId = facilityId,
                        QueryType = queryType,
                        IsSuccessful = resultBundle.Entry.All(x => x.Resource.TypeName != nameof(OperationOutcome)),
                        CreateDate = DateTime.UtcNow,
                        ModifyDate = DateTime.UtcNow
                    }, cancellationToken);

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
                            await _bundleResourceAcquiredEventService.GenerateEventAsync(resultBundle, new ResourceRequiredMessageRequest(facilityId, patientId, queryType, correlationId, new List<ScheduledReport> { report }), cancellationToken);

                        foreach (var entry in resultBundle.Entry)
                        {
                            await _queriedFhirResourceManager.AddAsync(new Domain.Entities.QueriedFhirResourceRecord
                            {
                                ResourceId = entry.Resource.Id,
                                ResourceType = entry.Resource.TypeName,
                                CorrelationId = correlationId,
                                PatientId = patientId,
                                FacilityId = facilityId,
                                QueryType = queryType,
                                IsSuccessful = resultBundle.Entry.All(x => x.Resource.TypeName != nameof(OperationOutcome)),
                                CreateDate = DateTime.UtcNow,
                                ModifyDate = DateTime.UtcNow
                            }, cancellationToken);

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
            _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; PatientId: {PatientId}", resourceType, patientId);
            throw;
        }


        if (readResource != null)
        {
            if (generateMessages)
                await _bundleResourceAcquiredEventService.GenerateEventAsync(new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = readResource } } }, new ResourceRequiredMessageRequest(facilityId, patientId, queryType, correlationId, new List<ScheduledReport> { report }), cancellationToken);

            await _queriedFhirResourceManager.AddAsync(new Domain.Entities.QueriedFhirResourceRecord
            {
                ResourceId = id,
                ResourceType = resourceType,
                CorrelationId = correlationId,
                PatientId = patientId,
                FacilityId = facilityId,
                QueryType = queryType,
                IsSuccessful = readResource is not OperationOutcome,
                CreateDate = DateTime.UtcNow,
                ModifyDate = DateTime.UtcNow
            }, cancellationToken);

            if (readResource is not OperationOutcome)
            {
                IncrementResourceAcquiredMetric(correlationId, patientId, facilityId, queryType, resourceType, id);
            }
        }


        return readResource;
    }

    private static string TEMPORARYPatientIdPart(string fullPatientUrl)
    {
        var separatedPatientUrl = fullPatientUrl.Split('/');
        var patientIdPart = string.Join("/", separatedPatientUrl.Skip(Math.Max(0, separatedPatientUrl.Length - 2)));
        return patientIdPart;
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

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
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

    public async Task<List<ResourceReference>> GetPagedBundledResultAndGenerateMessagesAsync(string baseUrl, string patientIdReference, string correlationId, string facilityId, string queryType, List<string> referenceTypes, PagedParameterQueryFactoryResult pagedQuery, ParameterQueryConfig config, ScheduledReport report, AuthenticationConfiguration authConfig)
    {
        List<ResourceReference> references = new List<ResourceReference>();

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
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

            var results = await SearchFhirEndpointAsync(parameters, fhirClient, config.ResourceType, patientIdReference, correlationId, facilityId, queryType, report, referenceTypes, true, false);
            references.AddRange(results.ResourceReference);
        }

        return references;
    }

    public async Task<List<ResourceReference>> GetSingularBundledResultsAndGenerateMessagesAsync(string baseUrl, string patientIdReference, string correlationId, string facilityId, string queryType, List<string> resourceTypes, SingularParameterQueryFactoryResult query, ParameterQueryConfig config, ScheduledReport report, AuthenticationConfiguration authConfig)
    {
        List<ResourceReference> references = new List<ResourceReference>();

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
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
            
            await _bundleResourceAcquiredEventService.GenerateEventAsync(new Bundle { Entry = new List<Bundle.EntryComponent> { new Bundle.EntryComponent { Resource = resource } } }, new ResourceRequiredMessageRequest(facilityId, patientIdReference, queryType, correlationId, new List<ScheduledReport> { report }));

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

            var result = await SearchFhirEndpointAsync(query.SearchParams, fhirClient, config.ResourceType, patientIdReference, correlationId, facilityId, queryType, report, resourceTypes, true, false);

            references.AddRange(result.ResourceReference);
        }

        return references;
    }
}
