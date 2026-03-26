using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.SerDes;

public class HealthStatusJsonConverter : JsonConverter<HealthStatus>
{
    public override HealthStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Convert the string to an enum, handle case-insensitive comparison
        if (Enum.TryParse(typeof(HealthStatus), reader.GetString(), true, out var type))
        {
            return (HealthStatus)type;
        }
        
        throw new JsonException("Invalid value for Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions.HealthStatus");
    }

    public override void Write(Utf8JsonWriter writer, HealthStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}