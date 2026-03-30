using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
public class TimeSpanConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                return XmlConvert.ToTimeSpan(value);
            }
            catch (Exception)
            {
                return TimeSpan.Parse(value);
            }
        }
        catch(Exception)
        {
            throw;
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        try
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(XmlConvert.ToString(value.Value));
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
