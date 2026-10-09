namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// A PayloadSubmitted retry is published back onto the main topic with the report key,
/// so it shares a partition with the original completions.
/// </summary>
public static class PayloadSubmittedRedrive
{
    public static bool Applies(string? topic) =>
        string.Equals(topic, nameof(KafkaTopic.PayloadSubmitted), StringComparison.Ordinal);

    public static string Key(string? storedKey, string? valueJson)
    {
        if (TryIds(storedKey, out var facilityId, out var reportScheduleId)
            || TryIds(valueJson, out facilityId, out reportScheduleId))
        {
            return KafkaKeys.ForReport(facilityId, reportScheduleId);
        }

        throw new InvalidOperationException("PayloadSubmitted redrive requires a facility id and a report schedule id.");
    }

    private static bool TryIds(string? text, out string facilityId, out Guid reportScheduleId)
    {
        var facility = KafkaKeyLegacy.TryReadFacility(text, out facilityId);
        var schedule = KafkaKeyLegacy.TryReadReportScheduleId(text, out reportScheduleId);
        return facility && schedule;
    }
}
