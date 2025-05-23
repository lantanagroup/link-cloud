using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public static class FhirResourceDeserializer
{
    public static DomainResource DeserializeFhirResource(ReferenceResources resource)
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

        DomainResource? domainResource = resource.ResourceType switch
        {
            nameof(Condition) => JsonSerializer.Deserialize<Condition>(resource.ReferenceResource, options),
            nameof(Coverage) => JsonSerializer.Deserialize<Coverage>(resource.ReferenceResource, options),
            nameof(Encounter) => JsonSerializer.Deserialize<Encounter>(resource.ReferenceResource, options),
            nameof(Location) => JsonSerializer.Deserialize<Location>(resource.ReferenceResource, options),
            nameof(Medication) => JsonSerializer.Deserialize<Medication>(resource.ReferenceResource, options),
            nameof(MedicationRequest) => JsonSerializer.Deserialize<MedicationRequest>(resource.ReferenceResource, options),
            nameof(Observation) => JsonSerializer.Deserialize<Observation>(resource.ReferenceResource, options),
            nameof(Patient) => JsonSerializer.Deserialize<Patient>(resource.ReferenceResource, options),
            nameof(Procedure) => JsonSerializer.Deserialize<Procedure>(resource.ReferenceResource, options),
            nameof(ServiceRequest) => JsonSerializer.Deserialize<ServiceRequest>(resource.ReferenceResource, options),
            nameof(Specimen) => JsonSerializer.Deserialize<Specimen>(resource.ReferenceResource, options),
            _ => throw new Exception($"Resource Type {resource.ResourceType} not configured for Read operation."),
        };

        return domainResource;
    }
}
