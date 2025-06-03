using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataAcquisition.Domain.Application.Serializers;
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            var value = reader.GetString();
            var output = TimeSpan.Parse(value);
            return output;
        }
        catch(Exception ex)
        {
            throw;
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        try
        {
            writer.WriteStringValue(value.ToString());
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
