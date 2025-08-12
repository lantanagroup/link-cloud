using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public class PatientIDsAcquiredDataSerializer<T> : ISerializer<T>
{
    public byte[] Serialize(T data, SerializationContext context)
    {
        return JsonSerializer.SerializeToUtf8Bytes(data);
    }
}
