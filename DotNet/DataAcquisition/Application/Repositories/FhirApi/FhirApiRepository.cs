using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Application.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.Auth;
using LantanaGroup.Link.DataAcquisition.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using MediatR;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories.FhirApi;

public class FhirApiRepository : IFhirApiRepository
{
    private readonly ILogger<FhirApiRepository> _logger;
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationRetrievalService _authenticationRetrievalService;
    private readonly IMediator _mediator;
    private readonly IQueriedFhirResourceRepository _queriedFhirResourceRepository;
    private readonly IDataAcquisitionServiceMetrics _metrics;

    public FhirApiRepository(ILogger<FhirApiRepository> logger, HttpClient httpClient, IAuthenticationRetrievalService authenticationRetrievalService, IMediator mediator, IQueriedFhirResourceRepository queriedFhirResourceRepository, IDataAcquisitionServiceMetrics metrics)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentException(nameof(authenticationRetrievalService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _queriedFhirResourceRepository = queriedFhirResourceRepository ?? throw new ArgumentNullException(nameof(queriedFhirResourceRepository));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task<Bundle> GetPagedBundledResultsAsync(
        string baseUrl,
        string patientIdReference,
        string correlationId,
        string facilityId,
        string queryType,
        Bundle bundle, 
        PagedParameterQueryFactoryResult pagedQuery, 
        ParameterQueryConfig config, 
        ScheduledReport report,
        AuthenticationConfiguration authConfig)
    {
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
            foreach(var b in resultBundle.Entry)
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
        Bundle bundle, 
        SingularParameterQueryFactoryResult query, 
        ParameterQueryConfig config, 
        ScheduledReport report,
        AuthenticationConfiguration authConfig)
    {
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
            resourceBundle.Entry.ForEach(x => 
            {
                if (!(x.Resource.TypeName == nameof(OperationOutcome)))
                {
                    bundle.AddResourceEntry(x.Resource, x.FullUrl);
                    IncrementResourceAcquiredMetric(patientIdReference, facilityId, queryType, x.Resource.TypeName);
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
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, "Patient")
        ]);

        var fhirClient = GenerateFhirClient(baseUrl);

        var authBuilderResults = await AuthMessageHandlerFactory.Build(_authenticationRetrievalService, authConfig);
        if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
        {
            fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
        }

        return (Patient)(await ReadFhirEndpointAsync(fhirClient, nameof(Patient), patientId, patientId, correlationId, facilityId, QueryPlanType.InitialQueries.ToString()));
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

    private async Task<Bundle> SearchFhirEndpointAsync(
        SearchParams searchParams, 
        FhirClient fhirClient, 
        string resourceType, 
        string? patientId = default, 
        string? correlationId = default, 
        string? facilityId = default,
        string? queryType = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType)
            ]);

            var resultBundle = await fhirClient.SearchAsync(searchParams, resourceType);
            await _queriedFhirResourceRepository.AddAsync(new Domain.Entities.QueriedFhirResourceRecord
            {
                ResourceId = resultBundle.Id,
                ResourceType = resourceType,
                CorrelationId = correlationId,
                PatientId = patientId,
                FacilityId = facilityId,
                QueryType = queryType,
                IsSuccessful = !resultBundle.Entry.Any(x => x.Resource.TypeName != nameof(OperationOutcome)),
            }, cancellationToken);           
            
            return resultBundle;
        }
        catch(FhirOperationException ex)
        {
            _logger.LogError(ex.Message, ex);
            return null;
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
        CancellationToken cancellationToken = default)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientId),
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType)
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
            _logger.LogError(ex, "error encountered retrieving fhir resource.");
            throw;
        }


        if (readResource != null)
        {
            await _queriedFhirResourceRepository.AddAsync(new Domain.Entities.QueriedFhirResourceRecord
            {
                ResourceId = id,
                ResourceType = resourceType,
                CorrelationId = correlationId,
                PatientId = patientId,
                FacilityId = facilityId,
                QueryType = queryType,
                IsSuccessful = readResource is not OperationOutcome,
            }, cancellationToken);

            if (readResource is not OperationOutcome)
            {
                IncrementResourceAcquiredMetric(patientId, facilityId, queryType, resourceType);
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
        List<ResourceReference> referenceIds, 
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
            foreach(var reference in referenceIds)
            {
                var refIdResult = GetRefId(reference, resourceType);

                if (!refIdResult.success)
                    continue;

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
        }
        else
        {
            foreach (var reference in referenceIds)
            {
                SearchParams searchParams = new SearchParams();
                try
                {
                    var id = (string.IsNullOrWhiteSpace(reference.ElementId) ? reference.Url.ToString() : reference.ElementId).Split("/").LastOrDefault();
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    searchParams.Add("_id", id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"No appropriate ID found for reference.\n{reference.ToJson()}");
                    continue;
                }
                
                if (authBuilderResults.isQueryParam)
                {
                    var kvPair = (AuthQueryKeyValuePair)authBuilderResults.authHeader;
                    searchParams.Add(kvPair.Key, kvPair.Value);
                }

                var result = await SearchFhirEndpointAsync(searchParams, fhirClient, resourceType, queryPlanType);
                if(result != null)
                {
                    domainResources.AddRange(result.Entry.Where(x => x.Resource is DomainResource && (x.Resource.TypeName != nameof(OperationOutcome))).Select(x => (DomainResource)x.Resource).ToList());
                }
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

    private void IncrementResourceAcquiredMetric(string? patientIdReference, string? facilityId, string? queryType, string resourceType)
    {
        _metrics.IncrementResourceAcquiredCounter([
            new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
            new KeyValuePair<string, object?>(DiagnosticNames.PatientId, patientIdReference), //TODO: Can we keep this?
            new KeyValuePair<string, object?>(DiagnosticNames.QueryType, queryType),
            new KeyValuePair<string, object?>(DiagnosticNames.Resource, resourceType)
        ]);
    }
}
