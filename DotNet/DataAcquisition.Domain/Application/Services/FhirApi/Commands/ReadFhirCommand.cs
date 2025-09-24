using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Domain.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public record ReadFhirCommandRequest(
    string facilityId,
    ResourceType resourceType,
    string resourceId,
    string baseUrl,
    FhirQueryConfiguration fhirQueryConfiguration);

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
        if (string.IsNullOrWhiteSpace(request.resourceId))
            throw new DeadLetterException("Resource ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(request.baseUrl))
            throw new DeadLetterException("FhirClient Endpoint cannot be null.");

        if (request.fhirQueryConfiguration == null)
            throw new DeadLetterException("FhirQueryConfiguration cannot be null.");

        const int maxLocalRetries = 3;
        Exception lastException = null;

        for (int attempt = 1; attempt <= maxLocalRetries; attempt++)
        {
            try
            {
                using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, request.fhirQueryConfiguration.MaxConcurrentRequests.Value, _distributedLockSettings.Expiration, cancellationToken))
                {
                    var fhirClient = new FhirClient(request.baseUrl.Trim('/'), _httpClient, new FhirClientSettings
                    {
                        PreferredFormat = ResourceFormat.Json
                    });

                    var authBuilderResults = await AuthMessageHandlerFactory.Build(request.facilityId, _authenticationRetrievalService, request.fhirQueryConfiguration.Authentication);
                    if (!authBuilderResults.isQueryParam && authBuilderResults.authHeader != null)
                    {
                        fhirClient.RequestHeaders.Authorization = (AuthenticationHeaderValue)authBuilderResults.authHeader;
                    }

                    string location = request.resourceType switch
                    {
                        ResourceType.List => $"List/{request.resourceId}",
                        _ => $"{request.resourceType}/{request.resourceId}"
                    };

                    var readResource = await fhirClient.ReadAsync<DomainResource>(location);

                    if (readResource == null)
                    {
                        throw new TransientException($"Resource not found. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}; Full location: {location}");
                    }

                    return readResource;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Error on attempt {Attempt}/{Max} for {ResourceType}/{ResourceId}, retrying...", attempt, maxLocalRetries, request.resourceType, request.resourceId);
                await Task.Delay((int)Math.Pow(2, attempt) * 1000, cancellationToken); // Exponential backoff: 1s, 2s, 4s
            }
        }

        // After max retries, decide what to throw based on the last exception
        _logger.LogError(lastException, "Max local retries ({Max}) exceeded for {ResourceType}/{ResourceId}", maxLocalRetries, request.resourceType, request.resourceId);
        if (IsDeadLetterError(lastException))
        {
            throw new DeadLetterException($"Error retrieving FHIR resource. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}", lastException);
        }

        throw new TransientException($"Max local retries ({maxLocalRetries}) exceeded for error retrieving {request.resourceType}/{request.resourceId}", lastException);
    }

    private bool IsDeadLetterError(Exception ex)
    {
        // Check for permanent errors (e.g., 4xx client errors)
        if (ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue)
        {
            return httpEx.StatusCode.Value >= HttpStatusCode.BadRequest && httpEx.StatusCode.Value < HttpStatusCode.InternalServerError; // 400-499
        }

        if (ex.InnerException is HttpRequestException innerHttpEx && innerHttpEx.StatusCode.HasValue)
        {
            return innerHttpEx.StatusCode.Value >= HttpStatusCode.BadRequest && innerHttpEx.StatusCode.Value < HttpStatusCode.InternalServerError;
        }

        if (ex is FhirOperationException fhirOpEx)
        {
            return fhirOpEx.Status >= HttpStatusCode.BadRequest && fhirOpEx.Status < HttpStatusCode.InternalServerError;
        }

        if (ex.InnerException is FhirOperationException innerFhirOpEx)
        {
            return innerFhirOpEx.Status >= HttpStatusCode.BadRequest && innerFhirOpEx.Status < HttpStatusCode.InternalServerError;
        }
        // Default to transient if status cannot be determined
        return false;
    }
}