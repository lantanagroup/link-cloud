using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

[JsonConverter(typeof(FhirVersionJsonConverter))]
public enum SftpAcquisitionType
{
    Census,
    CernerCensus,
    Resources
}

public static class SftpAcquisitionTypeProvider
{
    public static List<SftpAcquisitionType> GetSftpAcquisitionTypes()
    {
        return Enum.GetValues<SftpAcquisitionType>().ToList();
    }
}


public class FhirVersionJsonConverter : JsonConverter<SftpAcquisitionType>
{
    public override SftpAcquisitionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(Enum.TryParse<SftpAcquisitionType>(reader.GetString(), true, out var result))
        {
            return result;
        }
        
        throw new JsonException($"Unable to convert \"{reader.GetString()}\" to {nameof(SftpAcquisitionType)}.");
    }

    public override void Write(Utf8JsonWriter writer, SftpAcquisitionType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}