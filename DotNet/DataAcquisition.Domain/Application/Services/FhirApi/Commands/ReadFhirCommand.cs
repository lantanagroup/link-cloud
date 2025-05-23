using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

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

    public async Task<DomainResource> ExecuteAsync(ReadFhirCommandRequest request, CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(request.resourceId))
            throw new ArgumentNullException(nameof(request.resourceId), "Resource ID cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(request.baseUrl))
            throw new ArgumentNullException(nameof(request.baseUrl), "FhirClient Endpoint cannot be null.");

        using (_distributedSemaphoreProvider.AcquireSemaphore(request.facilityId, request.fhirQueryConfiguration.MaxConcurrentRequests, _distributedLockSettings.Expiration, cancellationToken))
        {
            var fhirClient = new FhirClient(request.baseUrl, _httpClient, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            try
            {
                var result = await fhirClient.GetAsync($"{request.baseUrl}/{request.resourceType}/{request.resourceId}", cancellationToken);

                if(result == null)
                {
                    throw new Exception($"Resource not found. ResourceType: {request.resourceType}; ResourceId: {request.resourceId}");
                }

                return (DomainResource)result;
                //return resourceType switch
                //{
                //    ResourceType.Condition => await fhirClient.ReadAsync<Condition>(resourceId),
                //    ResourceType.Coverage => await fhirClient.ReadAsync<Coverage>(resourceId),
                //    ResourceType.Encounter => await fhirClient.ReadAsync<Encounter>(resourceId),
                //    ResourceType.Location => await fhirClient.ReadAsync<Location>(resourceId),
                //    ResourceType.Medication => await fhirClient.ReadAsync<Medication>(resourceId),
                //    ResourceType.MedicationRequest => await fhirClient.ReadAsync<MedicationRequest>(resourceId),
                //    ResourceType.Observation => await fhirClient.ReadAsync<Observation>(resourceId),
                //    ResourceType.Patient => await fhirClient.ReadAsync<Patient>(resourceId.RemoveIdPathParts()),
                //    ResourceType.Procedure => await fhirClient.ReadAsync<Procedure>(resourceId),
                //    ResourceType.ServiceRequest => await fhirClient.ReadAsync<ServiceRequest>(resourceId),
                //    ResourceType.Specimen => await fhirClient.ReadAsync<Specimen>(resourceId),
                //    ResourceType.List => await fhirClient.ReadAsync<List>($"{fhirClient.Endpoint}/List/{resourceId}"),
                //    _ => throw new Exception($"Resource Type {resourceType} not configured for Read operation."),
                //};
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; ResourceId: {ResourceId}", request.resourceType, request.resourceId);
                throw;
            }
        }
    }
}
