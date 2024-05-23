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
            var jsonText = jsonDocument.RootElement.GetRawText();

            var converterOptions = new JsonSerializerOptions();
            converterOptions.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
            {
                DisableBase64Decoding = false,
                Validator = null,
                ValidateOnFailedParse = false
            });

            converterOptions.AllowTrailingCommas = true;
            converterOptions.PropertyNameCaseInsensitive = true;

            return JsonSerializer.Deserialize<T>(jsonText, converterOptions);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var converterOptions = new JsonSerializerOptions();
            converterOptions.ForFhir(ModelInfo.ModelInspector);

            converterOptions.AllowTrailingCommas = true;
            converterOptions.PropertyNameCaseInsensitive = true;

            writer.WriteStringValue(JsonSerializer.Serialize(value));
        }      
    }
}
