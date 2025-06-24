using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Net.Http.Headers;
using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using System.Diagnostics;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.ParameterQuery;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Domain.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Factory;
using ScheduledReport = LantanaGroup.Link.Shared.Application.Models.ScheduledReport;
using QueryPlanType = LantanaGroup.Link.DataAcquisition.Domain.Application.Models.QueryPlanType;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;

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
    private readonly IDataAcquisitionLogManager _dataAcquisitionLogManager;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest> _bundleResourceAcquiredEventService;
    private readonly IReferenceResourcesManager _referenceResourceManager;

    public FhirApiService(
        ILogger<FhirApiService> logger,
        HttpClient httpClient,
        IAuthenticationRetrievalService authenticationRetrievalService,
        IDataAcquisitionServiceMetrics metrics,
        IBundleEventService<string, ResourceAcquired, ResourceAcquiredMessageGenerationRequest> bundleResourceAcquiredEventService,
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
                Status = RequestStatus.Processing,
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
                log.Status = RequestStatus.Failed;
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
            log.Status = RequestStatus.Completed;
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
            Status = RequestStatus.Processing,
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

        if(string.IsNullOrWhiteSpace(id))
            throw new ArgumentNullException(nameof(id), "Resource ID cannot be null or empty.");

        if(fhirClient.Endpoint == null)
            throw new ArgumentNullException(nameof(fhirClient.Endpoint), "FhirClient Endpoint cannot be null.");

        if (resourceType.Equals("patient", StringComparison.InvariantCultureIgnoreCase))
            patientId = TEMPORARYPatientIdPart(patientId);

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
            stopWatch.Stop();
            log.CompletionDate = DateTime.UtcNow;
            log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
            log.Status = RequestStatus.Failed;
            log.ResourceAcquiredIds = new List<string>();
            await _dataAcquisitionLogManager.UpdateAsync(log, cancellationToken);

            _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; PatientId: {PatientId}", resourceType, patientId);
            throw;
        }

        stopWatch.Stop();
        log.CompletionDate = DateTime.UtcNow;
        log.CompletionTimeMilliseconds = stopWatch.ElapsedMilliseconds;
        log.Status = RequestStatus.Completed;
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
