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
/// Main topics, the composite key shape, and the consumer groups that subscribe to the topic
/// or its redrive topic. A topic that is not listed is treated as facility-keyed and
/// order-sensitive. Order-sensitive topics need a quiet window before a partition increase.
/// </summary>
public static class KafkaTopicCatalog
{
    public const string FacilityJsonShape = "{\"facilityId\":\"...\"}";
    public const string PatientJsonShape = "{\"facilityId\":\"...\",\"patientId\":\"...\"}";
    public const string PatientOrFacilityJsonShape = "{\"facilityId\":\"...\"} or {\"facilityId\":\"...\",\"patientId\":\"...\"}";
    public const string AuditShape = "patient JSON, facility JSON, or {serviceName}";
    public const string ServiceShape = "{serviceName}";

    public static IReadOnlyList<string> Slice1Topics { get; } =
    [
        "PatientCensusScheduled",
        "PatientListsAcquired",
        "CernerPatientsAcquired",
        "PatientEvent",
        "DataAcquisitionRequested",
        "ReadyToAcquire",
        "ResourcesAcquired",
        "MappingOutcomeEvaluated",
        "ResourcesNormalized",
        "EvaluationRequested",
        "MeasureReportGenerated",
        "ReadyForValidation",
        "ValidationComplete",
        "SubmitPayload",
        "PayloadSubmitted",
        "NotificationRequested"
    ];

    public static IReadOnlyList<KafkaTopicCatalogEntry> Topics { get; } =
    [
        Entry("AuditableEventOccurred", KafkaTopicKeyClass.Patient, orderSensitive: true, hardBlocked: false, AuditShape, "", "Audit"),
        Entry("CernerPatientsAcquired", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "Census"),
        Entry("DataAcquisitionRequested", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "DataAcquisition"),
        Entry("EvaluationRequested", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "measureeval", "measureeval-events"),
        Entry("GenerateReportRequested", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "Report"),
        Entry("MappingOutcomeEvaluated", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "Report"),
        Entry("MeasureReportGenerated", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "Report"),
        Entry("NotificationRequested", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "Notification"),
        Entry("PatientCensusScheduled", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "DataAcquisition"),
        Entry("PatientEvent", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "QueryDispatch", "Report"),
        Entry("PatientListsAcquired", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "Census"),
        Entry("PayloadSubmitted", KafkaTopicKeyClass.Patient, true, false, PatientOrFacilityJsonShape, "", "Report"),
        Entry("ReadyForValidation", KafkaTopicKeyClass.Patient, false, false, PatientJsonShape, "", "validation"),
        Entry("ReadyToAcquire", KafkaTopicKeyClass.Patient, false, false, PatientOrFacilityJsonShape, "", "DataAcquisitionWorker"),
        Entry("ReportScheduled", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, "", "QueryDispatch", "Report"),
        Entry("ResourcesAcquired", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "Normalization"),
        Entry("ResourcesNormalized", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "measureeval", "measureeval-events"),
        Entry("RetentionCheckScheduled", KafkaTopicKeyClass.Facility, true, false, FacilityJsonShape, ""),
        Entry("Service-Healthcheck", KafkaTopicKeyClass.Health, false, false, ServiceShape, ""),
        Entry("SubmitPayload", KafkaTopicKeyClass.Patient, true, false, PatientOrFacilityJsonShape, "", "Submission"),
        Entry("ValidationComplete", KafkaTopicKeyClass.Patient, true, false, PatientJsonShape, "", "Report")
    ];

    private static readonly Dictionary<string, KafkaTopicFamily> Families = BuildFamilies();

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

    public static KafkaTopicFamily FamilyOf(string? topic)
    {
        var main = MainName(topic);
        if (main.Length == 0)
            return new KafkaTopicFamily();
        return Families.TryGetValue(main, out var family) ? family : new KafkaTopicFamily { Topic = main, IneligibleReason = "The topic is not in the catalog." };
    }

    public static bool IsSlice1Eligible(string? topic) => FamilyOf(topic).Slice1Eligible;

    public static KafkaTopicKeyClass KeyClassOf(string? topic) =>
        Find(topic)?.KeyClass ?? KafkaTopicKeyClass.Facility;

    public static bool IsOrderSensitive(string? topic) =>
        Find(topic)?.OrderSensitive ?? true;

    public static bool IsHardBlocked(string? topic) =>
        Find(topic)?.HardBlocked == true;

    public static string KeyShapeOf(string? topic) =>
        Find(topic)?.KeyShape ?? FacilityJsonShape;

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
        var retry = name.IndexOf("-Retry", StringComparison.Ordinal);
        var redrive = name.IndexOf("-Redrive", StringComparison.Ordinal);
        var cut = -1;
        if (retry >= 0)
            cut = retry;
        if (redrive >= 0 && (cut < 0 || redrive < cut))
            cut = redrive;
        if (cut >= 0)
            return name[..cut];
        if (name.EndsWith("-Error", StringComparison.Ordinal))
            return name[..^"-Error".Length];
        return name;
    }

    public static string RetryName(string? topic) => MainName(topic) + "-Retry";

    public static string ErrorName(string? topic) => MainName(topic) + "-Error";

    public static string ServiceRetryName(string? topic, string service) => MainName(topic) + "-Retry-" + service;

    public static string ServiceRedriveName(string? topic, string service) => MainName(topic) + "-Redrive-" + service;

    public static bool IsRetry(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.Contains("-Retry", StringComparison.Ordinal);

    public static bool IsRedrive(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.Contains("-Redrive", StringComparison.Ordinal);

    public static bool IsError(string? topic) =>
        !string.IsNullOrWhiteSpace(topic) && topic.Contains("-Error", StringComparison.Ordinal) && !IsRetry(topic) && !IsRedrive(topic);

    public static string BackupTopicName(string topic, Guid migrationId)
    {
        var id = migrationId.ToString("N");
        var suffix = id.Length >= 8 ? id[..8] : id;
        return "_linkmig-" + MainName(topic) + "-" + suffix;
    }

    public const string JournalTopicName = "_linkmig-journal";

    public const string MigrationHeader = "x-link-migration";

    private static KafkaTopicCatalogEntry Entry(
        string topic,
        KafkaTopicKeyClass keyClass,
        bool orderSensitive,
        bool hardBlocked,
        string keyShape,
        string blockReason,
        params string[] groups) =>
        new(topic, keyClass, orderSensitive, hardBlocked, keyShape, blockReason, groups);

    private static Dictionary<string, KafkaTopicFamily> BuildFamilies()
    {
        var map = new Dictionary<string, KafkaTopicFamily>(StringComparer.Ordinal);
        Add(map, "AuditableEventOccurred", false, "Account and Tenant produce to this topic. Slice 2.", false, "Audit", "",
            Producers(
                Site("Account", "DotNet/Account/Application/Commands/AuditEvent/CreateAuditEvent.cs"),
                Site("Tenant", "DotNet/Tenant/Commands/CreateAuditEventCommand.cs"),
                Site("Notification", "DotNet/Notification/Application/Notification/Commands/CreateAuditEventCommand/CreateAuditEventCommand.cs"),
                Site("Report", "DotNet/Report/KafkaProducers/AuditableEventOccurredProducer.cs"),
                Site("Submission", "DotNet/Submission/KafkaProducers/AuditableEventOccurredProducer.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Jobs/QueryDispatchJob.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Listeners/PatientEventListener.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Listeners/ReportScheduledEventListener.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Domain/Managers/ScheduledReportManager.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Domain/Managers/QueryDispatchConfigurationManager.cs"),
                Site("QueryDispatch", "DotNet/QueryDispatch/Domain/Managers/PatientDispatchManager.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/KafkaOps/KafkaOpsService.cs", control: true)),
            Consumers(Site("Audit", "DotNet/Audit/Listeners/AuditEventListener.cs")));
        Add(map, "CernerPatientsAcquired", true, "", false, "Census", "",
            Producers(Site("DataAcquisition", "DotNet/DataAcquisition.Domain/Application/Services/Sftp/Processors/CernerCCLExtractProcessor.cs")),
            Consumers(Site("Census", "DotNet/Census/Listeners/CernerPatientsAcquiredListener.cs")));
        Add(map, "DataAcquisitionRequested", true, "", false, "DataAcquisition", "",
            Producers(
                Site("QueryDispatch", "DotNet/QueryDispatch/Jobs/QueryDispatchJob.cs"),
                Site("Report", "DotNet/Report/KafkaProducers/DataAcquisitionRequestedProducer.cs"),
                Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/services/AbstractResourceConsumer.java"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/CreateDataAcquisitionRequested/CreateDataAcquisitionRequested.cs", control: true)),
            Consumers(
                Site("DataAcquisition", "DotNet/DataAcquisition/Listeners/DataAcquisitionRequestedListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "EvaluationRequested", true, "", true, "", "",
            Producers(Site("Report", "DotNet/Report/Listeners/GenerateReportListener.cs")),
            Consumers(
                Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/services/EvaluationRequestedConsumer.java"),
                Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/KafkaConfig.java")));
        Add(map, "GenerateReportRequested", false, "Tenant produces to this topic. Slice 2.", false, "Report", "",
            Producers(Site("Tenant", "DotNet/Tenant/Controllers/FacilityController.cs")),
            Consumers(Site("Report", "DotNet/Report/Listeners/GenerateReportListener.cs")));
        Add(map, "MappingOutcomeEvaluated", true, "", false, "Report", "",
            Producers(
                Site("DataAcquisitionWorker", "DotNet/DataAcquisition.AcquisitionWorker/Services/AcquisitionProcessorBackgroundService.cs"),
                Site("Normalization", "DotNet/Normalization/Listeners/ResourcesAcquiredListener.cs")),
            Consumers(Site("Report", "DotNet/Report/Listeners/MappingOutcomeListener.cs")));
        Add(map, "MeasureReportGenerated", true, "", false, "Report", "",
            Producers(Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/services/MeasureReportGeneratedProducer.java")),
            Consumers(
                Site("Report", "DotNet/Report/Listeners/MeasureReportGeneratedListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "NotificationRequested", true, "", false, "", "Notification",
            Producers(),
            Consumers(Site("Notification", "DotNet/Notification/Listeners/NotificationRequestedListener.cs")));
        Add(map, "PatientCensusScheduled", true, "", false, "DataAcquisition", "",
            Producers(Site("Census", "DotNet/Census/Application/Jobs/SchedulePatientListRetrieval.cs")),
            Consumers(Site("DataAcquisition", "DotNet/DataAcquisition/Listeners/PatientCensusScheduledListener.cs")));
        Add(map, "PatientEvent", true, "", false, "QueryDispatch,Report", "",
            Producers(
                Site("Census", "DotNet/Census/Application/Services/EventProducerService.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/CreatePatientEvent/CreatePatientEvent.cs", control: true)),
            Consumers(
                Site("QueryDispatch", "DotNet/QueryDispatch/Listeners/PatientEventListener.cs"),
                Site("Report", "DotNet/Report/Listeners/PatientEventListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "PatientListsAcquired", true, "", false, "Census", "",
            Producers(
                Site("DataAcquisition", "DotNet/DataAcquisition.Domain/Application/Services/PatientCensusService.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/CreatePatientListAcquired/CreatePatientListAcquired.cs", control: true),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/CreatePatientAcquired/CreatePatientAcquired.cs", control: true)),
            Consumers(
                Site("Census", "DotNet/Census/Listeners/PatientListsAcquiredListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "PayloadSubmitted", true, "", false, "Report", "",
            Producers(Site("Submission", "DotNet/Submission/KafkaProducers/PayloadSubmittedProducer.cs")),
            Consumers(
                Site("Report", "DotNet/Report/Listeners/PayloadSubmittedListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ReadyForValidation", true, "", true, "", "",
            Producers(Site("Report", "DotNet/Report/KafkaProducers/ReadyForValidationProducer.cs")),
            Consumers(
                Site("validation", "Java/validation/src/main/java/com/lantanagroup/link/validation/services/ReadyForValidationConsumer.java"),
                Site("validation", "Java/validation/src/main/java/com/lantanagroup/link/validation/configs/KafkaConfig.java"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ReadyToAcquire", true, "", false, "", "DataAcquisitionWorker",
            Producers(
                Site("DataAcquisition", "DotNet/DataAcquisition/Jobs/AcquisitionProcessingJob.cs"),
                Site("DataAcquisition", "DotNet/DataAcquisition.Domain/Application/Services/DataAcquisitionLogService.cs")),
            Consumers(
                Site("DataAcquisitionWorker", "DotNet/DataAcquisition.AcquisitionWorker/Listeners/ReadyToAcquireListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ReportScheduled", false, "Tenant produces to this topic. Slice 2.", false, "Report,QueryDispatch", "",
            Producers(
                Site("Tenant", "DotNet/Tenant/Jobs/ReportScheduledJob.cs"),
                Site("DMRP", "DotNet/DMRP/Scheduling/DmrpNightlyJob.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/CreateReportScheduled/CreateReportScheduled.cs", control: true),
                Site("Link.UI", "DotNet/Automation.Link/Services/ReportApiHelper.cs", control: true)),
            Consumers(
                Site("QueryDispatch", "DotNet/QueryDispatch/Listeners/ReportScheduledEventListener.cs"),
                Site("Report", "DotNet/Report/Listeners/ReportScheduledListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ResourcesAcquired", true, "", false, "Normalization", "",
            Producers(
                Site("DataAcquisitionWorker", "DotNet/DataAcquisition.AcquisitionWorker/Services/AcquisitionProcessorBackgroundService.cs"),
                Site("DataAcquisition", "DotNet/DataAcquisition/Jobs/TailMessageRecoveryJob.cs")),
            Consumers(
                Site("Normalization", "DotNet/Normalization/Listeners/ResourcesAcquiredListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ResourcesNormalized", true, "", true, "", "",
            Producers(Site("Normalization", "DotNet/Normalization/Listeners/ResourcesAcquiredListener.cs")),
            Consumers(
                Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/services/ResourcesNormalizedConsumer.java"),
                Site("measureeval", "Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/KafkaConfig.java"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "RetentionCheckScheduled", false, "This topic has no consumer.", false, "", "",
            Producers(Site("Tenant", "DotNet/Tenant/Jobs/RetentionCheckScheduledJob.cs")),
            Consumers());
        Add(map, "Service-Healthcheck", false, "Health checks are not migrated.", false, "", "",
            Producers(Site("shared", "Java/shared/src/main/java/com/lantanagroup/link/shared/health/KafkaHealthCheckIndicator.java")),
            Consumers());
        Add(map, "SubmitPayload", true, "", false, "Submission", "",
            Producers(Site("Report", "DotNet/Report/KafkaProducers/SubmitPayLoadProducer.cs")),
            Consumers(
                Site("Submission", "DotNet/Submission/Listeners/SubmitPayloadListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        Add(map, "ValidationComplete", true, "", false, "Report", "",
            Producers(Site("validation", "Java/validation/src/main/java/com/lantanagroup/link/validation/services/ReadyForValidationConsumer.java")),
            Consumers(
                Site("Report", "DotNet/Report/Listeners/ValidationCompleteListener.cs"),
                Site("Admin.BFF", "DotNet/Admin.BFF/Application/Commands/Integration/KafkaConsumerManager.cs", control: true)));
        return map;
    }

    private static void Add(
        Dictionary<string, KafkaTopicFamily> map,
        string topic,
        bool slice1,
        string reason,
        bool javaPinned,
        string retryServices,
        string redriveOnly,
        IReadOnlyList<KafkaCodeSite> producers,
        IReadOnlyList<KafkaCodeSite> consumers)
    {
        map[topic] = new KafkaTopicFamily
        {
            Topic = topic,
            Slice1Eligible = slice1,
            IneligibleReason = reason,
            JavaPinnedSiblings = javaPinned,
            RetryServices = Split(retryServices),
            RedriveOnlyServices = Split(redriveOnly),
            Producers = producers,
            Consumers = consumers
        };
    }

    private static IReadOnlyList<string> Split(string value) =>
        value.Length == 0
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static KafkaCodeSite Site(string workload, string path, bool control = false) =>
        new(workload, path, control);

    private static IReadOnlyList<KafkaCodeSite> Producers(params KafkaCodeSite[] sites) => sites;

    private static IReadOnlyList<KafkaCodeSite> Consumers(params KafkaCodeSite[] sites) => sites;
}
