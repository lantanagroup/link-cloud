using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

public class FhirResourceConverter : JsonConverter<DomainResource>
{
    private readonly FhirJsonParser _fhirParser = new FhirJsonParser();

    public override DomainResource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            // Read the JSON string for the resource
            var json = JsonSerializer.Deserialize<JsonElement>(ref reader, options).GetRawText();
            return _fhirParser.Parse<DomainResource>(json);
        }
        catch (Exception ex)
        {
            throw new JsonException("Invalid FHIR resource payload.", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, DomainResource value, JsonSerializerOptions options)
    {
        // Serialize the FHIR resource to JSON
        var fhirSerializer = new FhirJsonSerializer();
        var json = fhirSerializer.SerializeToString(value);
        writer.WriteRawValue(json);
    }
}