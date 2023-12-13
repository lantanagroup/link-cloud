using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.MeasureEval.Models;
using System.Text.Json;

namespace LantanaGroup.Link.MeasureEval.Serializers;

public class PatientDataEvaluatedMessageSerializer<T> : ISerializer<T> where T : PatientDataEvaluatedMessage
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        var options = new JsonSerializerOptions().ForFhir(typeof(MeasureReport).Assembly);
        return JsonSerializer.SerializeToUtf8Bytes(data, options);
    }
}
