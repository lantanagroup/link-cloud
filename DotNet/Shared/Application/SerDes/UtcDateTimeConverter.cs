using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.SerDes
{
    public class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            DateTime dt = reader.GetDateTime();
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            else if (dt.Kind == DateTimeKind.Local)
            {
                return dt.ToUniversalTime();
            }
            return dt;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ssZ"));
        }
    }
}
