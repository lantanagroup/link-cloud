using Hl7.Fhir.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Shared.Application.SerDes;

namespace LantanaGroup.Link.Shared.Application.Converters
{
    public class FhirResourceConverter<T> : JsonConverter<T> where T : Hl7.Fhir.Model.Base
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var converterOptions = LinkFhirSerializerOptions.ForFhirWithoutValidation();            

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return JsonSerializer.Deserialize<T>(ref reader, converterOptions);
            }         
       
            // This is a string value, so we need to parse it as JSON
            using var jsonDocument = JsonDocument.ParseValue(ref reader);
            var jsonText = jsonDocument.RootElement.ToString().TrimStart('"').TrimEnd('"');

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
