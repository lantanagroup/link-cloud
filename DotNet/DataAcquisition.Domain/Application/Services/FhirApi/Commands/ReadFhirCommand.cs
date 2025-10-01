using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.Auth;

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
    }
}
