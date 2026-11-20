using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation.UI.Models;

public sealed class TestRailCaseIdJsonConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (!reader.TryGetInt32(out var number) || number <= 0)
                    throw new JsonException("TestRail case id must be a positive integer.");
                return number;
            case JsonTokenType.String:
                if (!TestRailCaseIdParser.TryParse(reader.GetString(), out var parsed))
                    throw new JsonException("TestRail case id must be a positive integer.");
                return parsed;
            default:
                throw new JsonException("TestRail case id must be a positive integer.");
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteNumberValue(value.Value);
    }
}
