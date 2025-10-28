using Confluent.Kafka;
using System.Text.Json;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;
public class IndentedJsonSerializer<T> : ISerializer<T>
{
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    public byte[] Serialize(T data, SerializationContext context)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToUtf8Bytes(data, _options);
    }
}
