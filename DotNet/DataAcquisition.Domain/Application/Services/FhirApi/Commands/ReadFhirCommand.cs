using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Application.Domain.Factories.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

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
            throw new ArgumentNullException(nameof(request.resourceId), "Resource ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(request.baseUrl))
            throw new ArgumentNullException(nameof(request.baseUrl), "FhirClient Endpoint cannot be null.");


        if (request.fhirQueryConfiguration == null)
            throw new ArgumentNullException(nameof(request.fhirQueryConfiguration), "FhirQueryConfiguration cannot be null.");

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

                try
                {
                    string location = request.resourceType switch
                    {
                        ResourceType.List => $"List/{request.resourceId}",
                        //ResourceType.Patient => TEMPORARYPatientIdPart(id),
                        _ => $"{request.resourceType}/{request.resourceId}"
                    };

                    var readResource = await fhirClient.ReadAsync<DomainResource>(location);

                    if (readResource == null)
                    {
                        throw new Exception($"Resource not found. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}; Full location: {location}");
                    }

                    return readResource;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; ResourceId: {ResourceId}", request.resourceType, request.resourceId);
                    throw new FhirApiFetchFailureException($"error encountered retrieving fhir resource. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}");
                }
            }
        }
        catch (TimeoutException dlEx)
        {
            _logger.LogError(dlEx, "An error occurred while attempting to fetch a lock for facilityId {facilityId} while processing a Read FHIR request.", request.facilityId.Sanitize());
            throw new FhirApiFetchFailureException($"A deadlock occurred while processing a Read FHIR request for facilityId: {request.facilityId}, ResourceType: {request.resourceType}, ResourceId: {request.resourceId}. Please see Logs for more details.");
        }
    }
}
