using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Shared.Application.Utilities;

public class KafkaHeaderHelper
{
    private static string? GetHeaderByKey(Headers headers, string key)
    {
        if (headers.TryGetLastBytes(key, out var value))
            return Encoding.UTF8.GetString(value);
        return null;
    }
    
    public static string? GetExceptionFacilityId(Headers headers) => GetHeaderByKey(headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);
    
    public static string? GetCorrelationId(Headers headers) => GetHeaderByKey(headers, KafkaConstants.HeaderConstants.CorrelationId);
}