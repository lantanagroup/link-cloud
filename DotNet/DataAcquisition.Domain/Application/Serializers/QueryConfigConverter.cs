using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public class QueryConfigConverter : JsonConverter<IQueryConfig>
{
    public override IQueryConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            if (!doc.RootElement.TryGetProperty("QueryConfigType", out JsonElement typeElement))
            {
                if (!doc.RootElement.TryGetProperty("queryConfigType", out typeElement))
                {
                    throw new JsonException("Missing QueryConfigType property.");
                }
            }
            var configType = typeElement.GetString();
            return configType switch
            {
                "Parameter" => JsonSerializer.Deserialize<ParameterQueryConfig>(doc.RootElement.GetRawText(), options),
                "Reference" => JsonSerializer.Deserialize<ReferenceQueryConfig>(doc.RootElement.GetRawText(), options),
                _ => throw new JsonException($"Unknown QueryConfigType: {configType}")
            };
        }
    }
    public override void Write(Utf8JsonWriter writer, IQueryConfig value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}