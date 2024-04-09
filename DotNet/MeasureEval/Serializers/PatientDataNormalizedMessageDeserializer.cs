using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.MeasureEval.Models;
using System.Text.Json;

namespace LantanaGroup.Link.MeasureEval.Serializers;

public class PatientDataNormalizedMessageDeserializer<T> : IDeserializer<T> where T : PatientDataNormalizedMessage
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        string jsonContent = System.Text.Encoding.Default.GetString(data.ToArray());
        var options = new JsonSerializerOptions() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        var patientDataNormalizedMessage = JsonSerializer.Deserialize<T>(jsonContent, options);
        var ele = ((JsonElement)patientDataNormalizedMessage.PatientBundle).ToString();
        var bundle = new FhirJsonParser(new ParserSettings { AcceptUnknownMembers = true, PermissiveParsing = true }).Parse<Bundle>(ele);
        patientDataNormalizedMessage.PatientBundle = bundle;
        return patientDataNormalizedMessage;
    }
}
