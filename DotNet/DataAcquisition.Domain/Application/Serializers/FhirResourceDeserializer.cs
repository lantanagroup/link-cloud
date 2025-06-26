using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public static class FhirResourceDeserializer
{
    public static Resource DeserializeFhirResource(ReferenceResources resource)
    {
        var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

        return JsonSerializer.Deserialize<Resource>(resource.ReferenceResource, options);
    }
}
