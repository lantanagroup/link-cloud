using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.SerDes;

public class LinkServiceHealthStatusJsonConverter : JsonConverter<LinkServiceHealthStatus>
{
    public override LinkServiceHealthStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Convert the string to an enum, handle case-insensitive comparison
        if (Enum.TryParse(typeof(LinkServiceHealthStatus), reader.GetString(), true, out var type))
        {
            return (LinkServiceHealthStatus)type;
        }
        
        throw new JsonException("Invalid value for LinkServiceHealthStatus");
    }

    public override void Write(Utf8JsonWriter writer, LinkServiceHealthStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}