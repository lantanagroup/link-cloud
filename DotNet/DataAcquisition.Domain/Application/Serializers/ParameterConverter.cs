using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public class ParameterConverter : JsonConverter<IParameter>
{
    public override IParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            JsonElement typeElement;
            string configType = null;

            if (doc.RootElement.TryGetProperty("ParameterType", out typeElement) ||
                doc.RootElement.TryGetProperty("parameterType", out typeElement))
            {
                configType = typeElement.GetString();
            }
            else if (doc.RootElement.TryGetProperty("$type", out typeElement))
            {
                var typeName = typeElement.GetString();
                if (typeName?.Contains("LiteralParameter") == true)
                {
                    configType = "Literal";
                }
                else if (typeName?.Contains("ResourceIdsParameter") == true)
                {
                    configType = "ResourceIds";
                }
                else if (typeName?.Contains("VariableParameter") == true)
                {
                    configType = "Variable";
                }
            }

            if (configType == null)
            {
                // Fallback to property inspection
                if (doc.RootElement.TryGetProperty("Literal", out _))
                {
                    configType = "Literal";
                }
                else if (doc.RootElement.TryGetProperty("Resource", out _) && doc.RootElement.TryGetProperty("Paged", out _))
                {
                    configType = "ResourceIds";
                }
                else if (doc.RootElement.TryGetProperty("Variable", out _))
                {
                    configType = "Variable";
                }
                else
                {
                    throw new JsonException("Unable to determine ParameterType. Missing type discriminator or distinguishing properties.");
                }
            }

            return configType switch
            {
                "Literal" => JsonSerializer.Deserialize<LiteralParameter>(doc.RootElement.GetRawText(), options),
                "ResourceIds" => JsonSerializer.Deserialize<ResourceIdsParameter>(doc.RootElement.GetRawText(), options),
                "Variable" => JsonSerializer.Deserialize<VariableParameter>(doc.RootElement.GetRawText(), options),
                _ => throw new JsonException($"Unknown ParameterType: {configType}")
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, IParameter value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}