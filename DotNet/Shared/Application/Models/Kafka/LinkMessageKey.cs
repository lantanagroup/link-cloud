using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Partition key. Producers emit <see cref="Serialize"/> and consumers may read it with
/// <see cref="TryDeserialize"/>. Facility, patient, and report identifiers that business
/// logic needs are also on the message value.
/// </summary>
public sealed record LinkMessageKey(string FacilityId, string? PatientId)
{
    public string Serialize()
    {
        if (string.IsNullOrEmpty(FacilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(FacilityId));
        }

        var facility = Escape(FacilityId);
        if (string.IsNullOrEmpty(PatientId))
        {
            return "{\"facilityId\":\"" + facility + "\"}";
        }

        return "{\"facilityId\":\"" + facility + "\",\"patientId\":\"" + Escape(PatientId) + "\"}";
    }

    /// <summary>
    /// Report-scoped key. A patient id does not belong here: the completion stream for one
    /// report has to stay on one partition, and a patient id would split it.
    /// </summary>
    public static string SerializeReport(string facilityId, Guid reportScheduleId)
    {
        if (string.IsNullOrEmpty(facilityId))
        {
            throw new ArgumentException("Facility id is required.", nameof(facilityId));
        }

        if (reportScheduleId == Guid.Empty)
        {
            throw new ArgumentException("Report schedule id is required.", nameof(reportScheduleId));
        }

        return "{\"facilityId\":\"" + Escape(facilityId) + "\",\"reportScheduleId\":\"" + reportScheduleId.ToString("D") + "\"}";
    }

    public static bool TryDeserialize(string? key, out LinkMessageKey? messageKey)
    {
        messageKey = null;
        if (string.IsNullOrWhiteSpace(key) || !key.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(key);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? facilityId = null;
            string? patientId = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (string.Equals(property.Name, "facilityId", StringComparison.OrdinalIgnoreCase))
                {
                    facilityId = property.Value.GetString();
                }
                else if (string.Equals(property.Name, "patientId", StringComparison.OrdinalIgnoreCase))
                {
                    patientId = property.Value.GetString();
                }
            }

            if (string.IsNullOrEmpty(facilityId))
            {
                return false;
            }

            messageKey = new LinkMessageKey(facilityId, string.IsNullOrEmpty(patientId) ? null : patientId);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static void RejectUnpairedSurrogates(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (char.IsHighSurrogate(character))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    throw new ArgumentException("Identifier contains an unpaired surrogate.");
                }

                i++;
                continue;
            }

            if (char.IsLowSurrogate(character))
            {
                throw new ArgumentException("Identifier contains an unpaired surrogate.");
            }
        }
    }

    private static string Escape(string value)
    {
        RejectUnpairedSurrogates(value);
        var builder = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
