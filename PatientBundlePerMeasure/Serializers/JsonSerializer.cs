using Confluent.Kafka;
using Streamiz.Kafka.Net.SerDes;
using System.Text.Json;

namespace LantanaGroup.Link.PatientBundlePerMeasure.Serializers
{
    public class JsonSerializer<T> : ISerDes<T>
    {
        public T Deserialize(byte[] data, SerializationContext context)
        {
            throw new NotImplementedException();
        }

        public object DeserializeObject(byte[] data, SerializationContext context)
        {
            string jsonContent = System.Text.Encoding.Default.GetString(data.ToArray());
            var options = new JsonSerializerOptions();
            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }

        public void Initialize(SerDesContext context)
        {
            //throw new NotImplementedException();
        }

        public byte[] Serialize(T data, SerializationContext context)
        {
            var options = new JsonSerializerOptions();
            return JsonSerializer.SerializeToUtf8Bytes(data, options);
        }

        public byte[] SerializeObject(object data, SerializationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
