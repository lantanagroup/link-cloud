using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public static class FhirResourceDeserializer
{
    private static readonly JsonSerializerOptions options =
        new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    public static Resource DeserializeFhirResource(ReferenceResourcesModel resource)
    {
        return JsonSerializer.Deserialize<Resource>(resource.ReferenceResource, options);
    }
}
