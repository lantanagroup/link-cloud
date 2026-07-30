using System.Net;
using System.Net.Http.Headers;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public record ReadFhirCommandRequest(
    string facilityId,
    ResourceType resourceType,
    string resourceId,
    string baseUrl,
    FhirQueryConfigurationModel fhirQueryConfiguration,
    string? reportTrackingId);

public interface IReadFhirCommand
{
    Task<DomainResource> ExecuteAsync(
        ReadFhirCommandRequest request,
        CancellationToken cancellationToken = default);
}

public class ReadFhirCommand : IReadFhirCommand
{
    private readonly ILogger<ReadFhirCommand> _logger;
    private readonly HttpClient _httpClient;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;
    private readonly DistributedLockSettings _distributedLockSettings;
    private readonly IAuthenticationRetrievalService _authenticationRetrievalService;

    public ReadFhirCommand(
        ILogger<ReadFhirCommand> logger,
        HttpClient httpClient,
        IDistributedSemaphoreProvider distributedSemaphoreProvider,
        IOptions<DistributedLockSettings> distributedLockSettings,
        IAuthenticationRetrievalService authenticationRetrievalService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient), "HttpClient cannot be null.");
        _distributedSemaphoreProvider = distributedSemaphoreProvider ?? throw new ArgumentNullException(nameof(distributedSemaphoreProvider), "Distributed semaphore provider cannot be null.");
        _distributedLockSettings = distributedLockSettings.Value ?? throw new ArgumentNullException(nameof(distributedLockSettings), "Distributed lock settings cannot be null.");
        _authenticationRetrievalService = authenticationRetrievalService ?? throw new ArgumentNullException(nameof(authenticationRetrievalService), "Authentication retrieval service cannot be null.");
    }

    public async Task<DomainResource> ExecuteAsync(ReadFhirCommandRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("ReadFhirCommand.ExecuteAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, request.facilityId);
        activity?.SetTag(DiagnosticNames.ResourceType, request.resourceType.ToString());
        activity?.SetTag(DiagnosticNames.ResourceId, request.resourceId);
        activity?.SetTag(DiagnosticNames.ReportTrackingId, request.reportTrackingId);

        if (string.IsNullOrWhiteSpace(request.resourceId))
            throw new ArgumentNullException(nameof(request.resourceId), "Resource ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(request.baseUrl))
            throw new ArgumentNullException(nameof(request.baseUrl), "FhirClient Endpoint cannot be null.");

        if (request.fhirQueryConfiguration == null)
            throw new ArgumentNullException(nameof(request.fhirQueryConfiguration), "FhirQueryConfiguration cannot be null.");

        var maxConcurrent = request.fhirQueryConfiguration.GetMaxConcurrentRequestsOrDefault();
        var semWaitStart = DateTime.UtcNow;
        var maskedFacilityId = request.facilityId.MaskForLog();
        var maskedResourceId = request.resourceId.MaskForLog();
        _logger.LogDebug(
            "Semaphore: Read acquire attempt facility={FacilityId} resource={ResourceType}/{ResourceId} maxConcurrent={MaxConcurrent}",
            maskedFacilityId, request.resourceType, maskedResourceId, maxConcurrent);
        using (await _distributedSemaphoreProvider.AcquireSemaphoreAsync(request.facilityId, maxConcurrent, _distributedLockSettings.Expiration, cancellationToken))
        {
            var semAcquiredAt = DateTime.UtcNow;
            _logger.LogDebug(
                "Semaphore: Read acquired facility={FacilityId} resource={ResourceType}/{ResourceId} waitMs={WaitMs}",
                maskedFacilityId, request.resourceType, maskedResourceId, (long)(semAcquiredAt - semWaitStart).TotalMilliseconds);
            // Create a new handler chain using a DelegatingHandler around a base HttpClientHandler
            var innerHandler = new HttpClientHandler();
            var headerCapturingHandler = new HeaderCapturingHandler { InnerHandler = innerHandler };
            var httpClientWithHandler = new HttpClient(headerCapturingHandler);

            var fhirClient = new FhirClient(request.baseUrl.Trim('/'), httpClientWithHandler, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.fhirQueryConfiguration.Authentication);
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

            string location = request.resourceType switch
            {
                ResourceType.List => $"List/{request.resourceId}",
                //ResourceType.Patient => TEMPORARYPatientIdPart(id),
                _ => $"{request.resourceType}/{request.resourceId}"
            };

            DomainResource readResource;
            try
            {
                readResource = await fhirClient.ReadAsync<DomainResource>(location);
            }
            catch (FhirOperationException ex) when (ex.Status == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = FhirCommandUtils.ParseRetryAfter(headerCapturingHandler.LastResponseHeaders);
                throw new TooManyRequestsException($"Too many requests for {location}", retryAfter);
            }

            if (readResource == null)
            {
                throw new Exception($"Resource not found. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}; Full location: {location}");
            }

            _logger.LogDebug(
                "Semaphore: Read releasing facility={FacilityId} resource={ResourceType}/{ResourceId} holdMs={HoldMs}",
                maskedFacilityId, request.resourceType, maskedResourceId, (long)(DateTime.UtcNow - semAcquiredAt).TotalMilliseconds);
            return readResource;
        }
    }
}