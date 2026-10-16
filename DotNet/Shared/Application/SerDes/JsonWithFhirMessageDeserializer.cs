using Confluent.Kafka;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.SerDes
{
    public class JsonWithFhirMessageDeserializer<T> : IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            if (isNull)
                return default(T);

            string jsonContent = System.Text.Encoding.Default.GetString(data.ToArray());

            return JsonSerializer.Deserialize<T>(jsonContent, LinkFhirSerializerOptions.ForFhirLenientSerialization);
        }
    }
}
