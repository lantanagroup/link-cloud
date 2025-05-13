using LantanaGroup.Link.Normalization.Application.Operations;
using System.Text.Json;
using System.Text.Json.Serialization;

public class OperationConverter : JsonConverter<IOperation>
{
    public override IOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            if (!doc.RootElement.TryGetProperty("OperationType", out JsonElement typeElement))
            {
                throw new JsonException("Missing operationType property.");
            }

            string operationType = typeElement.GetString();

            return operationType switch
            {
                "CopyProperty" => JsonSerializer.Deserialize<CopyPropertyOperation>(doc.RootElement.GetRawText(), options),
                //"CodeMap" => JsonSerializer.Deserialize<CodeMapOperation>(doc.RootElement.GetRawText(), options),
                _ => throw new JsonException($"Unknown operationType: {operationType}")
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, IOperation value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}