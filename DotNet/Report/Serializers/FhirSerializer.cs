using Confluent.Kafka;
using Hl7.Fhir.Serialization;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Serializers
{
    public class FhirSerializer<T> : ISerializer<T>, IDeserializer<T>
    {
        public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            var options = new JsonSerializerOptions().ForFhir();
            return JsonSerializer.Deserialize<T>(data, options);
        }

        public byte[] Serialize(T data, SerializationContext context)
        {
            var options = new JsonSerializerOptions().ForFhir();
            return JsonSerializer.SerializeToUtf8Bytes(data, options);
        }
    }
}
