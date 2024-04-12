using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Serializers;

public class PatientDataAcquiredMessageDeserializer<T> : IDeserializer<T> where T : PatientDataAcquiredMessage
{
    public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        string jsonContent = System.Text.Encoding.Default.GetString(data.ToArray());
        var options = new JsonSerializerOptions() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        var patientDataAcquiredMessage = JsonSerializer.Deserialize<T>(jsonContent, options);
        var ele = ((JsonElement)patientDataAcquiredMessage.PatientBundle).ToString();
        Bundle bundle = null;
        try
        {
            bundle = JsonSerializer.Deserialize<Bundle>(ele, new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector));
        }
        catch (DeserializationFailedException ex)
        {
            bundle = (Bundle)ex?.PartialResult;
        }

        patientDataAcquiredMessage.PatientBundle = bundle;
        return patientDataAcquiredMessage;
    }
}
