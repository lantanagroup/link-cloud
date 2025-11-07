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
            JsonElement typeElement;
            string configType = null;

            if (doc.RootElement.TryGetProperty("QueryConfigType", out typeElement) ||
                doc.RootElement.TryGetProperty("queryConfigType", out typeElement))
            {
                configType = typeElement.GetString();
            }
            else if (doc.RootElement.TryGetProperty("$type", out typeElement))
            {
                var typeName = typeElement.GetString();
                if (typeName?.Contains("ParameterQueryConfig") == true)
                {
                    configType = "Parameter";
                }
                else if (typeName?.Contains("ReferenceQueryConfig") == true)
                {
                    configType = "Reference";
                }
            }

            if (configType == null)
            {
                // Fallback to property inspection if no type discriminator is found
                if (doc.RootElement.TryGetProperty("Parameters", out _))
                {
                    configType = "Parameter";
                }
                else if (doc.RootElement.TryGetProperty("Paged", out _))
                {
                    configType = "Reference";
                }
                else
                {
                    throw new JsonException("Unable to determine QueryConfigType. Missing type discriminator or distinguishing properties.");
                }
            }

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