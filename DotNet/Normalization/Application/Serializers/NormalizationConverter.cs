using LantanaGroup.Link.Normalization.Application.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Serializers;

public class NormalizationConverter : JsonConverter<NormalizationConfigModel>
{
    public override NormalizationConfigModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            JsonElement root = doc.RootElement;
            var model = NormalizationConfigModelDeserializer.Deserialize(root);
            return model;
        }
    }

    public override void Write(Utf8JsonWriter writer, NormalizationConfigModel value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}


