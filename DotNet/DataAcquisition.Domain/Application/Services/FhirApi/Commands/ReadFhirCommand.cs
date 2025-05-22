using DataAcquisition.Domain.Extensions;
using DataAcquisition.Domain.Infrastructure.Entities;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;

public interface IReadFhirCommand 
{     
    Task<DomainResource> ExecuteAsync(
        string facilityId,
        ResourceType resourceType,
        string resourceId,
        string baseUrl,
        FhirQueryConfiguration fhirQueryConfiguration,
        CancellationToken cancellationToken = default);
}
public class ReadFhirCommand : IReadFhirCommand
{
    private readonly ILogger<ReadFhirCommand> _logger;
    private readonly HttpClient _httpClient;
    private readonly IDistributedSemaphoreProvider _distributedSemaphoreProvider;
    private readonly DistributedLockSettings _distributedLockSettings;

    public async Task<DomainResource> ExecuteAsync(string facilityId, ResourceType resourceType, string resourceId, string baseUrl, FhirQueryConfiguration fhirQueryConfiguration, CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentNullException(nameof(resourceId), "Resource ID cannot be null or empty.");

        if (baseUrl == null)
            throw new ArgumentNullException(nameof(baseUrl), "FhirClient Endpoint cannot be null.");

        using (_distributedSemaphoreProvider.AcquireSemaphore(facilityId, fhirQueryConfiguration.MaxConcurrentRequests, _distributedLockSettings.Expiration, cancellationToken))
        {
            var fhirClient = new FhirClient(baseUrl, _httpClient, new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json
            });

            try
            {
                var result = await fhirClient.GetAsync($"{baseUrl}/{resourceType}/{resourceId}", cancellationToken);

                if(result == null)
                {
                    throw new Exception($"Resource not found. ResourceType: {resourceType}; ResourceId: {resourceId}");
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
                _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; ResourceId: {ResourceId}", resourceType, resourceId);
                throw;
            }
        }
    }
}
