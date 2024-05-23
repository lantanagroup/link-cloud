using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Model;

namespace LantanaGroup.Link.Shared.Application.Converters
{
    public class FhirResourceConverter<T> : JsonConverter<T> where T : Hl7.Fhir.Model.Base
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var jsonDocument = JsonDocument.ParseValue(ref reader);
            var jsonText = jsonDocument.RootElement.ToString().TrimStart('"').TrimEnd('"');

            var converterOptions = new JsonSerializerOptions();
            converterOptions.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
            {
                DisableBase64Decoding = false,
                Validator = null,

            });

            converterOptions.AllowTrailingCommas = true;
            converterOptions.PropertyNameCaseInsensitive = true;

            var resource = JsonSerializer.Deserialize<T>(jsonText, converterOptions);

            return resource;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var fhirSerializer = new FhirJsonSerializer();
            writer.WriteStringValue(fhirSerializer.SerializeToString(value));
        }
    }
}
