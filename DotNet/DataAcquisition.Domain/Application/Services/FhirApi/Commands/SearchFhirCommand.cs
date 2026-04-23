using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public record SearchFhirCommandRequest(
    FhirQueryConfigurationModel queryConfig,
    ResourceType resourceType,
    SearchParams searchParams,
    string? facilityId,
    string? patientId,
    string? correlationId,
    QueryPhase? queryPhase,
    FhirQueryType queryType
    );

public interface ISearchFhirCommand
{
    IAsyncEnumerable<Bundle> ExecuteAsync(
        SearchFhirCommandRequest request,
        CancellationToken cancellationToken = default);
    Task<Bundle> ExecuteNonPagingAsync(
        SearchFhirCommandRequest request,
        CancellationToken cancellationToken = default);
}

public class SearchFhirCommand : ISearchFhirCommand
{
    private readonly ILogger<SearchFhirCommand> _logger;
    private readonly HttpClient _httpClient;
    private readonly IDataAcquisitionServiceMetrics _metrics;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;
    private readonly DistributedLockSettings _distributedLockSettings;
    private readonly IAuthenticationRetrievalService _authenticationRetrievalService;

    public SearchFhirCommand(ILogger<SearchFhirCommand> logger, HttpClient httpClient, IDataAcquisitionServiceMetrics metrics, IDistributedSemaphoreProvider distributedSemaphoreProvider, IOptions<DistributedLockSettings> distributedLockSettings, IAuthenticationRetrievalService authenticationRetrievalService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _distributedSemaphoreProvider = distributedSemaphoreProvider ?? throw new ArgumentNullException(nameof(distributedSemaphoreProvider));
        _distributedLockSettings = distributedLockSettings?.Value ?? throw new ArgumentNullException(nameof(distributedLockSettings));
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentNullException(nameof(authenticationRetrievalService));
    }

    public async IAsyncEnumerable<Bundle> ExecuteAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("SearchFhirCommand.ExecuteAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, request.facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, request.correlationId);
        activity?.SetTag(DiagnosticNames.PatientId, request.patientId);
        activity?.SetTag(DiagnosticNames.QueryType, request.queryPhase?.ToString());
        activity?.SetTag(DiagnosticNames.ResourceType, request.resourceType.ToString());

        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        if (request == null || string.IsNullOrWhiteSpace(request.facilityId) || string.IsNullOrWhiteSpace(request.queryConfig.FhirServerBaseUrl))
        {
            _logger.LogError("Invalid request parameters. FacilityId: {FacilityId}; FhirServerBaseUrl: {FhirServerBaseUrl}", request?.facilityId?.Sanitize(), request?.queryConfig.FhirServerBaseUrl.Sanitize());
            yield break;
        }

        // Create a new handler chain using a DelegatingHandler around a base HttpClientHandler
        var innerHandler = new HttpClientHandler();
        var headerCapturingHandler = new HeaderCapturingHandler { InnerHandler = innerHandler };
        var httpClientWithHandler = new HttpClient(headerCapturingHandler);

        var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, httpClientWithHandler, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        });

        Bundle? resultBundle = null;

        var maxConcurrent = request.queryConfig.GetMaxConcurrentRequestsOrDefault();
        var semWaitStart = DateTime.UtcNow;
        var maskedFacilityId = request.facilityId.MaskForLog();
        _logger.LogDebug(
            "Semaphore: SearchPaging acquire attempt facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} maxConcurrent={MaxConcurrent}",
            maskedFacilityId, request.resourceType, request.correlationId, maxConcurrent);
        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, maxConcurrent, _distributedLockSettings.Expiration, cancellationToken))
        {
            var semAcquiredAt = DateTime.UtcNow;
            _logger.LogDebug(
                "Semaphore: SearchPaging acquired facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} waitMs={WaitMs}",
                maskedFacilityId, request.resourceType, request.correlationId, (long)(semAcquiredAt - semWaitStart).TotalMilliseconds);

            var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.queryConfig.Authentication);
            if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
            {
                if (authBuilderResults.authHeader is AuthenticationHeaderValue authHeaderValue)
                {
                    fhirClient.RequestHeaders.Authorization = authHeaderValue;
                }
                else if (authBuilderResults.authHeader is Dictionary<string, string> customHeaders)
                {
                    foreach (var header in customHeaders)
                    {
                        fhirClient.RequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }

            try
            {
                if (request.queryType == FhirQueryType.SearchPost)
                {
                    resultBundle = await fhirClient.SearchUsingPostAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
                }
                else
                {
                    resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
                }
            }
            catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = FhirCommandUtils.ParseRetryAfter(headerCapturingHandler.LastResponseHeaders);
                throw new TooManyRequestsException($"Too many requests for search on {request.resourceType}", retryAfter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encountered while searching FHIR resources. ResourceType: {ResourceType}; FacilityId: {facilityId};", request.resourceType, request.facilityId.Sanitize());
                throw;
            }

            _logger.LogDebug(
                "Semaphore: SearchPaging releasing facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} holdMs={HoldMs}",
                maskedFacilityId, request.resourceType, request.correlationId, (long)(DateTime.UtcNow - semAcquiredAt).TotalMilliseconds);
        }

        if (resultBundle != null)
        {
            yield return resultBundle;
            IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);

            while (resultBundle.Link.Exists(x => x.Relation == "next"))
            {
                try
                {
                    _logger.LogDebug(
                        "Semaphore: SearchPaging acquire attempt facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} maxConcurrent={MaxConcurrent}",
                        maskedFacilityId, request.resourceType, request.correlationId, maxConcurrent);
                    using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, maxConcurrent, _distributedLockSettings.Expiration, cancellationToken))
                    {
                        var semAcquiredAt = DateTime.UtcNow;
                        _logger.LogDebug(
                            "Semaphore: SearchPaging acquired facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} waitMs={WaitMs}",
                            maskedFacilityId, request.resourceType, request.correlationId, (long)(semAcquiredAt - semWaitStart).TotalMilliseconds);

                        resultBundle = await fhirClient.ContinueAsync(resultBundle, ct: cancellationToken);

                        _logger.LogDebug(
                            "Semaphore: SearchPaging releasing facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} holdMs={HoldMs}",
                            maskedFacilityId, request.resourceType, request.correlationId, (long)(DateTime.UtcNow - semAcquiredAt).TotalMilliseconds);
                    }
                }
                catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = FhirCommandUtils.ParseRetryAfter(headerCapturingHandler.LastResponseHeaders);
                    throw new TooManyRequestsException($"Too many requests during paging for {request.resourceType}", retryAfter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error encountered while searching FHIR resources. ResourceType: {ResourceType}; SearchParams: {SearchParams},\n\n\t{stack}\n\n\t{innerStack}", request.resourceType, request.searchParams, ex.StackTrace, ex.InnerException?.StackTrace);
                    throw;
                }

                if (resultBundle != null)
                {
                    yield return resultBundle;
                    IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
                }
                else
                {
                    yield break;
                }
            }
        }
    }

    public async Task<Bundle> ExecuteNonPagingAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("SearchFhirCommand.ExecuteNonPagingAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, request.facilityId);
        activity?.SetTag(DiagnosticNames.CorrelationId, request.correlationId);
        activity?.SetTag(DiagnosticNames.PatientId, request.patientId);
        activity?.SetTag(DiagnosticNames.QueryType, request.queryPhase?.ToString());
        activity?.SetTag(DiagnosticNames.ResourceType, request.resourceType.ToString());

        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        var maxConcurrent = request.queryConfig.GetMaxConcurrentRequestsOrDefault();
        var semWaitStart = DateTime.UtcNow;
        var maskedFacilityId = request.facilityId.MaskForLog();
        _logger.LogDebug(
            "Semaphore: SearchNonPaging acquire attempt facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} maxConcurrent={MaxConcurrent}",
            maskedFacilityId, request.resourceType, request.correlationId, maxConcurrent);
        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, maxConcurrent, _distributedLockSettings.Expiration, cancellationToken))
        {
            var semAcquiredAt = DateTime.UtcNow;
            _logger.LogDebug(
                "Semaphore: SearchNonPaging acquired facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} waitMs={WaitMs}",
                maskedFacilityId, request.resourceType, request.correlationId, (long)(semAcquiredAt - semWaitStart).TotalMilliseconds);

            // Create a new handler chain using a DelegatingHandler around a base HttpClientHandler
            var innerHandler = new HttpClientHandler();
            var headerCapturingHandler = new HeaderCapturingHandler { InnerHandler = innerHandler };
            var httpClientWithHandler = new HttpClient(headerCapturingHandler);

            var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, httpClientWithHandler, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.queryConfig.Authentication);
            if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
            {
                if (authBuilderResults.authHeader is AuthenticationHeaderValue authHeaderValue)
                {
                    fhirClient.RequestHeaders.Authorization = authHeaderValue;
                }
                else if (authBuilderResults.authHeader is Dictionary<string, string> customHeaders)
                {
                    foreach (var header in customHeaders)
                    {
                        fhirClient.RequestHeaders.Add(header.Key, header.Value);
                    }
                }
            }

            Bundle resultBundle;
            try
            {
                resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
            }
            catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = FhirCommandUtils.ParseRetryAfter(headerCapturingHandler.LastResponseHeaders);
                throw new TooManyRequestsException($"Too many requests for non-paging search on {request.resourceType}", retryAfter);
            }
            IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
            _logger.LogDebug(
                "Semaphore: SearchNonPaging releasing facility={FacilityId} resource={ResourceType} correlationId={CorrelationId} holdMs={HoldMs}",
                maskedFacilityId, request.resourceType, request.correlationId, (long)(DateTime.UtcNow - semAcquiredAt).TotalMilliseconds);
            return resultBundle;
        }
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
}