using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

[JsonConverter(typeof(SftpAcquisitionSubTypeJsonConverter))]
public enum SftpAcquisitionSubType
{
    None,
    CernerCCLExtract
}

public static class SftpAcquisitionSubTypeProvider
{
    public static List<SftpAcquisitionSubType> GetSftpAcquisitionSubTypes()
    {
        return Enum.GetValues<SftpAcquisitionSubType>().ToList();
    }
}

public class SftpAcquisitionSubTypeJsonConverter : JsonConverter<SftpAcquisitionSubType>
{
    public override SftpAcquisitionSubType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (Enum.TryParse<SftpAcquisitionSubType>(reader.GetString(), true, out var result))
        {
            return result;
        }

        throw new JsonException($"Unable to convert \"{reader.GetString()}\" to {nameof(SftpAcquisitionSubType)}.");
    }

    public override void Write(Utf8JsonWriter writer, SftpAcquisitionSubType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
