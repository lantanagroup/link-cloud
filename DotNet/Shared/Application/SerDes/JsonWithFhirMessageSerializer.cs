using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.SerDes
{
    public class JsonWithFhirMessageSerializer<T> : ISerializer<T>
    {
        public byte[] Serialize(T data, SerializationContext context)
        {
            try
            {
                var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
                return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, options);
            }
            catch
            {
                //do nothing
            }

            try
            {
                var serializedData = JsonConvert.SerializeObject(data);
                return Encoding.UTF8.GetBytes(serializedData);
            }
            catch
            {
                throw;
            }
        }
    }
}
