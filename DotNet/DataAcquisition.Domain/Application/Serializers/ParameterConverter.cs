using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig.Parameter;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public class ParameterConverter : JsonConverter<IParameter>
{
    public override IParameter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            if (!doc.RootElement.TryGetProperty("ParameterType", out JsonElement typeElement))
            {
                if (!doc.RootElement.TryGetProperty("parameterType", out typeElement))
                {
                    throw new JsonException("Missing ParameterType property.");
                }
            }
            var configType = typeElement.GetString();
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