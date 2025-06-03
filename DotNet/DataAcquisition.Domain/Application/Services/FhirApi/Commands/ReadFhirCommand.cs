using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public ReadFhirCommand(
        ILogger<ReadFhirCommand> logger, 
        HttpClient httpClient, 
        IDistributedSemaphoreProvider distributedSemaphoreProvider, 
        IOptions<DistributedLockSettings> distributedLockSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null.");
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient), "HttpClient cannot be null.");
        _distributedSemaphoreProvider = distributedSemaphoreProvider ?? throw new ArgumentNullException(nameof(distributedSemaphoreProvider), "Distributed semaphore provider cannot be null.");
        _distributedLockSettings = distributedLockSettings.Value ?? throw new ArgumentNullException(nameof(distributedLockSettings), "Distributed lock settings cannot be null.");
    }

    public async Task<DomainResource> ExecuteAsync(ReadFhirCommandRequest request, CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(request.resourceId))
            throw new ArgumentNullException(nameof(request.resourceId), "Resource ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(request.baseUrl))
            throw new ArgumentNullException(nameof(request.baseUrl), "FhirClient Endpoint cannot be null.");

        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, request.fhirQueryConfiguration.MaxConcurrentRequests.Value, _distributedLockSettings.Expiration, cancellationToken))
        {
            var fhirClient = new FhirClient(request.baseUrl, _httpClient, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            try
            {
                string location = request.resourceType switch
                {
                    ResourceType.List => $"{fhirClient.Endpoint}/List/{request.resourceId}",
                    //ResourceType.Patient => TEMPORARYPatientIdPart(id),
                    _ => request.resourceId
                };

                var readResource = await fhirClient.ReadAsync<DomainResource>(location);

                if (readResource == null)
                {
                    throw new Exception($"Resource not found. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}");
                }

                return readResource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; ResourceId: {ResourceId}", request.resourceType, request.resourceId);
                throw;
            }
        }
    }
}
