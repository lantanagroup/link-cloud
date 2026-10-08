namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public enum KafkaTopicKeyClass
{
    Facility,
    Patient,
    Log,
    Report,
    Correlation,
    Unkeyed,
    Health
}

public sealed record KafkaTopicCatalogEntry(
    string Topic,
    KafkaTopicKeyClass KeyClass,
    bool OrderSensitive,
    bool HardBlocked,
    string KeyShape,
    string BlockReason,
    IReadOnlyList<string> Groups);

/// <summary>
/// Main topics, the key shape, and the consumer groups that subscribe to the topic
/// or its retry topic. A topic that is not listed is treated as facility-keyed and
/// order-sensitive. Order-sensitive topics need a quiet window before a partition
/// increase. DataAcquisitionRequested stays blocked while .NET and Java hash keys
/// differently.
/// </summary>
public static class KafkaTopicCatalog
{
    public static IReadOnlyList<KafkaTopicCatalogEntry> Topics { get; } =
    [
        Entry("AuditableEventOccurred", KafkaTopicKeyClass.Facility, orderSensitive: true, hardBlocked: false, "{facilityId}", "", "Audit"),
        Entry("CernerPatientsAcquired", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "Census"),
        Entry("DataAcquisitionRequested", KafkaTopicKeyClass.Facility, true, true, "{facilityId}",
            "DataAcquisitionRequested is produced by .NET with CRC32 and by MeasureEval with murmur2. The same facility key can land on different partitions. Partition changes stay blocked until every .NET producer uses murmur2.",
            "DataAcquisition"),
        Entry("EvaluationRequested", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "measureeval", "measureeval-events"),
        Entry("GenerateReportRequested", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "Report"),
        Entry("MappingOutcomeEvaluated", KafkaTopicKeyClass.Patient, true, false, "{facilityId}:{patientId}", "", "Report"),
        Entry("MeasureReportGenerated", KafkaTopicKeyClass.Unkeyed, true, false, "null", "", "Report"),
        Entry("NotificationRequested", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "Notification"),
        Entry("PatientCensusScheduled", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "DataAcquisition"),
        Entry("PatientEvent", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "QueryDispatch", "Report"),
        Entry("PatientListsAcquired", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "Census"),
        Entry("PayloadSubmitted", KafkaTopicKeyClass.Report, true, false, "{facilityId}:{reportScheduleId}", "", "Report"),
        Entry("ReadyForValidation", KafkaTopicKeyClass.Correlation, false, false, "{facilityId}:{correlationId}", "", "validation"),
        Entry("ReadyToAcquire", KafkaTopicKeyClass.Log, false, false, "{logId}", "", "DataAcquisitionWorker"),
        Entry("ReportScheduled", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "QueryDispatch", "Report"),
        Entry("ResourcesAcquired", KafkaTopicKeyClass.Patient, true, false, "{facilityId}:{patientId}", "", "Normalization"),
        Entry("ResourcesNormalized", KafkaTopicKeyClass.Patient, true, false, "{facilityId}:{patientId}", "", "measureeval", "measureeval-events"),
        Entry("RetentionCheckScheduled", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", ""),
        Entry("Service-Healthcheck", KafkaTopicKeyClass.Health, false, false, "{serviceName}", ""),
        Entry("SubmitPayload", KafkaTopicKeyClass.Report, true, false, "{facilityId}:{reportScheduleId}", "", "Submission"),
        Entry("ValidationComplete", KafkaTopicKeyClass.Facility, true, false, "{facilityId}", "", "Report")
    ];

    public static KafkaTopicCatalogEntry? Find(string? topic)
    {
        var main = MainName(topic);
        if (main.Length == 0)
            return null;

        foreach (var entry in Topics)
        {
            if (string.Equals(entry.Topic, main, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    public static KafkaTopicKeyClass KeyClassOf(string? topic) =>
        Find(topic)?.KeyClass ?? KafkaTopicKeyClass.Facility;

    public static bool IsOrderSensitive(string? topic) =>
        Find(topic)?.OrderSensitive ?? true;

    public static bool IsHardBlocked(string? topic) =>
        Find(topic)?.HardBlocked == true;

    public static string KeyShapeOf(string? topic) =>
        Find(topic)?.KeyShape ?? "{facilityId}";

    public static string BlockReasonOf(string? topic) =>
        Find(topic)?.BlockReason ?? "";

    public static bool AllowsIncreaseWithoutQuietWindow(KafkaTopicKeyClass keyClass) =>
        keyClass is KafkaTopicKeyClass.Log or KafkaTopicKeyClass.Correlation or KafkaTopicKeyClass.Health;

    public static IReadOnlyList<string> GroupsOf(string? topic) =>
        Find(topic)?.Groups ?? [];

    public static string MainName(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return "";

        var name = topic.Trim();
        if (name.EndsWith("-Retry", StringComparison.Ordinal))
            return name[..^"-Retry".Length];
        if (name.EndsWith("-Error", StringComparison.Ordinal))
            return name[..^"-Error".Length];
        return name;
    }

    public static string RetryName(string? topic) => MainName(topic) + "-Retry";

    public static string ErrorName(string? topic) => MainName(topic) + "-Error";

    public static bool IsRetry(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.EndsWith("-Retry", StringComparison.Ordinal);

    public static bool IsError(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.EndsWith("-Error", StringComparison.Ordinal);

    private static KafkaTopicCatalogEntry Entry(
        string topic,
        KafkaTopicKeyClass keyClass,
        bool orderSensitive,
        bool hardBlocked,
        string keyShape,
        string blockReason,
        params string[] groups) =>
        new(topic, keyClass, orderSensitive, hardBlocked, keyShape, blockReason, groups);
}
