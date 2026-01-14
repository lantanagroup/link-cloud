using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.SerDes;

public class ResourceTypeJsonConverter : JsonConverter<ResourceType>
{
    public override ResourceType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Convert the string to an enum, handle case-insensitive comparison
        if (Enum.TryParse(typeof(ResourceType), reader.GetString(), true, out var type))
        {
            return (ResourceType)type;
        }
        
        throw new JsonException("Invalid value for HL7.Fhir.R4.ResourceType");
    }

    public override void Write(Utf8JsonWriter writer, ResourceType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}