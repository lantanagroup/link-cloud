using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.Shared.Application.SerDes;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public static class FhirResourceDeserializer
{
    public static Resource DeserializeFhirResource(ReferenceResourcesModel resource)
    {
        return JsonSerializer.Deserialize<Resource>(resource.ReferenceResource, LinkFhirSerializerOptions.ForFhirLenientSerialization);
    }
}
