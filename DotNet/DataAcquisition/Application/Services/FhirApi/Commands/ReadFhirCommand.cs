using DataAcquisition.Domain.Extensions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace LantanaGroup.Link.DataAcquisition.Application.Services.FhirApi.Commands;

public interface IReadFhirCommand 
{     
    Task<DomainResource> ExecuteAsync(
        string facilityId,
        ResourceType resourceType,
        string resourceId,
        string baseUrl,
        CancellationToken cancellationToken = default);
}
public class ReadFhirCommand : IReadFhirCommand
{
    private readonly ILogger<ReadFhirCommand> _logger;
    private readonly HttpClient _httpClient;

    public async Task<DomainResource> ExecuteAsync(string facilityId, ResourceType resourceType, string resourceId, string baseUrl, CancellationToken cancellationToken = default)
    {
        var fhirClient = new FhirClient(baseUrl, _httpClient, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json
        });

        try
        {
            return resourceType switch
            {
                ResourceType.Condition => await fhirClient.ReadAsync<Condition>(resourceId),
                ResourceType.Coverage => await fhirClient.ReadAsync<Coverage>(resourceId),
                ResourceType.Encounter => await fhirClient.ReadAsync<Encounter>(resourceId),
                ResourceType.Location => await fhirClient.ReadAsync<Location>(resourceId),
                ResourceType.Medication => await fhirClient.ReadAsync<Medication>(resourceId),
                ResourceType.MedicationRequest => await fhirClient.ReadAsync<MedicationRequest>(resourceId),
                ResourceType.Observation => await fhirClient.ReadAsync<Observation>(resourceId),
                ResourceType.Patient => await fhirClient.ReadAsync<Patient>(resourceId.RemoveIdPathParts()),
                ResourceType.Procedure => await fhirClient.ReadAsync<Procedure>(resourceId),
                ResourceType.ServiceRequest => await fhirClient.ReadAsync<ServiceRequest>(resourceId),
                ResourceType.Specimen => await fhirClient.ReadAsync<Specimen>(resourceId),
                ResourceType.List => await fhirClient.ReadAsync<List>($"{fhirClient.Endpoint}/List/{resourceId}"),
                _ => throw new Exception($"Resource Type {resourceType} not configured for Read operation."),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "error encountered retrieving fhir resource. ResourceType: {ResourceType}; ResourceId: {ResourceId}", resourceType, resourceId);
            throw;
        }
    }
}
