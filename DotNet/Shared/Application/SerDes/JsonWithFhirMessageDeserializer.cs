using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
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
            
            var options = new JsonSerializerOptions();

            options.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
            {
                DisableBase64Decoding = false,
                Validator = null
            });           

            options.AllowTrailingCommas = true;
            options.PropertyNameCaseInsensitive = true;

            return JsonSerializer.Deserialize<T>(jsonContent, options);
        }
    }
}
