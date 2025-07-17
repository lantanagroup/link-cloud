using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Domain.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Services;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using ResourceType = Hl7.Fhir.Model.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public record SearchFhirCommandRequest(
    FhirQueryConfiguration queryConfig,
    ResourceType resourceType,
    SearchParams searchParams,
    string? facilityId,
    string? patientId,
    string? correlationId,
    QueryPhase? queryPhase);

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
        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        if(request == null || string.IsNullOrWhiteSpace(request.facilityId) || string.IsNullOrWhiteSpace(request.queryConfig.FhirServerBaseUrl))
        {
            _logger.LogError("Invalid request parameters. FacilityId: {FacilityId}; FhirServerBaseUrl: {FhirServerBaseUrl}", request?.facilityId, request?.queryConfig.FhirServerBaseUrl);
            yield break;
        }


        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, request.queryConfig.MaxConcurrentRequests.Value, _distributedLockSettings.Expiration, cancellationToken))
        {

            var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, _httpClient, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.queryConfig.Authentication);
            if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
            {
                fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
            }

            Bundle? resultBundle = null;

            try
            {
                resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error encountered while searching FHIR resources. ResourceType: {ResourceType}; SearchParams: {SearchParams},\n\n\t{stack}\n\n\t{innerStack}", request.resourceType, request.searchParams, ex.StackTrace, ex.InnerException?.StackTrace);
                yield break;
            }

            yield return resultBundle;

            Bundle? newResultBundle = resultBundle;

            if (newResultBundle != null)
            {
                while (resultBundle.Link.Exists(x => x.Relation == "next"))
                {
                    try
                    {
                        resultBundle = await fhirClient.ContinueAsync(resultBundle, ct: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered while searching FHIR resources. ResourceType: {ResourceType}; SearchParams: {SearchParams},\n\n\t{stack}\n\n\t{innerStack}", request.resourceType, request.searchParams, ex.StackTrace, ex.InnerException?.StackTrace);
                        yield break;
                    }

                    if (resultBundle != null && resultBundle.Entry.Any())
                    {
                        yield return resultBundle;
                        IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
                    }
                }
            }
        }

    }

    public async Task<Bundle> ExecuteNonPagingAsync(SearchFhirCommandRequest request, CancellationToken cancellationToken)
    {
        using var _ = _metrics.MeasureDataRequestDuration([
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, request.facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.PatientId, request.patientId),
                new KeyValuePair<string, object?>(DiagnosticNames.QueryType, request.queryPhase),
                new KeyValuePair<string, object?>(DiagnosticNames.CorrelationId, request.correlationId),
                new KeyValuePair<string, object?>(DiagnosticNames.Resource, request.resourceType)
            ]);

        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, request.queryConfig.MaxConcurrentRequests.Value, _distributedLockSettings.Expiration, cancellationToken))
        {
            var fhirClient = new FhirClient(request.queryConfig.FhirServerBaseUrl, _httpClient, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.queryConfig.Authentication);
            if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
            {
                fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
            }

            var resultBundle = await fhirClient.SearchAsync(request.searchParams, request.resourceType.ToString(), cancellationToken);
            IncrementResourceAcquiredMetric(request.correlationId, request.patientId, request.facilityId, request.queryPhase.ToString(), request.resourceType.ToString(), resultBundle.Id);
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
