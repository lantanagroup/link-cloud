using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Reads facility, patient, and report ids from a pipeline record.
/// JSON keys from <see cref="LinkMessageKey"/> win. A plain key is a facility id, or a
/// facility and patient split by '|' or the unit separator, except health and service-name keys.
/// </summary>
public static class KafkaBrowseDecoder
{
    private const char UnitSeparator = '\u001f';

    public static KafkaBrowseLink Decode(string? topic, string? keyText, string? valueText, IEnumerable<KafkaBrowseHeader>? headers)
    {
        var link = new KafkaBrowseLink();
        var (facility, patient) = DecodeKey(topic, keyText);
        link.FacilityId = facility;
        link.PatientId = patient;
        ApplyValue(link, valueText);
        ApplyHeaders(link, headers);
        return link;
    }

    public static (string? FacilityId, string? PatientId) DecodeKey(string? topic, string? keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
            return (null, null);

        var key = keyText.Trim();
        if (LinkMessageKey.TryDeserialize(key, out var parsed) && parsed is not null)
            return (parsed.FacilityId, string.IsNullOrEmpty(parsed.PatientId) ? null : parsed.PatientId);

        if (key.StartsWith('{'))
            return (null, null);

        if (IsServiceNameKey(topic, key))
            return (null, null);

        if (TrySplit(key, out var facilityId, out var patientId))
            return (facilityId, patientId);

        return (key, null);
    }

    public static bool TryUtf8(byte[]? bytes, out string text)
    {
        text = "";
        if (bytes is null || bytes.Length == 0)
            return true;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string HexPreview(byte[] bytes)
    {
        var take = Math.Min(bytes.Length, 24);
        var hex = Convert.ToHexString(bytes.AsSpan(0, take));
        return bytes.Length > take ? hex + "…" : hex;
    }

    public static string Summary(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= 120 ? flat : flat[..120] + "…";
    }

    public static string? Pretty(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                return null;
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsServiceNameKey(string? topic, string key)
    {
        var entry = KafkaTopicCatalog.Find(topic);
        if (entry is null)
            return false;
        if (entry.KeyClass == KafkaTopicKeyClass.Health || entry.KeyShape == KafkaTopicCatalog.ServiceShape)
            return true;
        if (entry.KeyShape != KafkaTopicCatalog.AuditShape)
            return false;
        return !key.Contains('|') && !key.Contains(UnitSeparator);
    }

    private static bool TrySplit(string key, out string facilityId, out string? patientId)
    {
        facilityId = "";
        patientId = null;
        var index = key.IndexOf('|');
        var unit = key.IndexOf(UnitSeparator);
        if (unit >= 0 && (index < 0 || unit < index))
            index = unit;
        if (index <= 0 || index >= key.Length - 1)
            return false;
        facilityId = key[..index];
        patientId = key[(index + 1)..];
        return facilityId.Length > 0 && patientId.Length > 0;
    }

    private static void ApplyValue(KafkaBrowseLink link, string? valueText)
    {
        if (string.IsNullOrWhiteSpace(valueText) || !valueText.TrimStart().StartsWith('{'))
            return;
        try
        {
            using var document = JsonDocument.Parse(valueText);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;
                var text = property.Value.GetString();
                if (string.IsNullOrEmpty(text))
                    continue;
                if (Is(property.Name, "facilityId") && string.IsNullOrEmpty(link.FacilityId))
                    link.FacilityId = text;
                else if (Is(property.Name, "patientId") && string.IsNullOrEmpty(link.PatientId))
                    link.PatientId = text;
                else if (Is(property.Name, "reportId"))
                    link.ReportId = text;
                else if (Is(property.Name, "adhocReportId"))
                    link.AdhocReportId = text;
            }
        }
        catch (JsonException)
        {
            // A value that is not JSON still shows as text. Ids stay on the key.
        }
    }

    private static void ApplyHeaders(KafkaBrowseLink link, IEnumerable<KafkaBrowseHeader>? headers)
    {
        if (headers is null)
            return;
        foreach (var header in headers)
        {
            if (string.IsNullOrEmpty(header.Name))
                continue;
            var name = header.Name;
            var value = header.Value ?? "";
            if (Is(name, KafkaConstants.HeaderConstants.CorrelationId))
                link.CorrelationId = value;
            else if (Is(name, KafkaConstants.HeaderConstants.RetryCount))
                link.RetryCount = value;
            else if (Is(name, KafkaConstants.HeaderConstants.ExceptionService))
                link.ExceptionService = value;
            else if (Is(name, KafkaConstants.HeaderConstants.ExceptionMessage))
                link.ExceptionMessage = value;
            else if (Is(name, KafkaConstants.HeaderConstants.RetryExceptionMessage))
                link.RetryExceptionMessage = value;
            else if (Is(name, KafkaConstants.HeaderConstants.ExceptionFacilityId))
            {
                link.ExceptionFacilityId = value;
                if (string.IsNullOrEmpty(link.FacilityId))
                    link.FacilityId = value;
            }
            else if (Is(name, KafkaConstants.HeaderConstants.ExceptionPartition))
                link.ExceptionPartition = value;
            else if (Is(name, KafkaConstants.HeaderConstants.ExceptionOffset))
                link.ExceptionOffset = value;

            if (name.StartsWith("X-Exception-", StringComparison.OrdinalIgnoreCase)
                || Is(name, KafkaConstants.HeaderConstants.RetryExceptionMessage))
            {
                link.ExceptionHeaders.Add(new KafkaBrowseHeader { Name = name, Value = value });
            }
        }
    }

    private static bool Is(string name, string expected) =>
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
}
