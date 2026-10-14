namespace LantanaGroup.Link.Operations.Application.KafkaOps;

public sealed class KafkaOpsOptions
{
    public const string SectionName = "KafkaOps";

    public int MaxPartitionsPerTopic { get; set; } = 24;

    /// <summary>
    /// Two-person approval. On (the default): every change request needs a second approver who is a different,
    /// signed-in person, and the console refuses approval when the two people cannot be told apart.
    /// Off only for local development, where the requester approves and executes their own request.
    /// </summary>
    public bool TwoPersonApproval { get; set; } = true;

    public bool RequireSecondApprover { get; set; }
    public bool RequireDifferentApprover { get; set; } = true;

    /// <summary>Whether a new request waits for a second approval.</summary>
    public bool SecondApprover => TwoPersonApproval || RequireSecondApprover;

    /// <summary>Whether the approver and executor must be a different, identifiable person from the requester.</summary>
    public bool DifferentApprover => TwoPersonApproval || RequireDifferentApprover;

    /// <summary>
    /// When the configuration value is absent, the host treats Production as read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    public int MetadataRefreshIntervalMs { get; set; } = 30_000;

    public int ExpectedConfigVersion { get; set; } = 1;

    public int CacheSeconds { get; set; } = 8;

    public int VerificationWindowSeconds { get; set; } = 900;

    public int RateLimitMinutes { get; set; } = 30;

    public int ConvergenceAlertMultiple { get; set; } = 3;

    public string InfraProvider { get; set; } = "Disabled";

    public int ScaleTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// A request still Executing this long after its last save is treated as left behind by a replica that stopped,
    /// and is resumed or closed by the executor that holds the lease.
    /// </summary>
    public int ReconcileExecutingAfterSeconds { get; set; } = 300;

    /// <summary>Closed requests older than this are left out of the history list. Open requests are always listed.</summary>
    public int HistoryRetentionDays { get; set; } = 30;

    public string ComposeProject { get; set; } = "";

    public string ComposeFile { get; set; } = "";

    public string BrokerServicePrefix { get; set; } = "broker-";

    public string ExtraBrokerProfile { get; set; } = "extra-broker";

    public string ExtraBrokerService { get; set; } = "broker-3";

    public string KubernetesNamespace { get; set; } = "kafka";

    public string KafkaNodePool { get; set; } = "";

    public string ConsumerDeployments { get; set; } = "";

    public string BrokerService { get; set; } = "broker-0";
    public string KafkaToolsPath { get; set; } = "/opt/kafka/bin";
    public int BrokerInternalPort { get; set; } = 9092;
    public string BrokerServices { get; set; } = "";

    public string ConsumerService { get; set; } = "consumer";

    /// <summary>Replication throttle in bytes per second for rebalance and decommission moves. 0 leaves moves unthrottled.</summary>
    public int ReassignmentThrottleBytesPerSecond { get; set; }

    /// <summary>Broker add, rebalance and decommission, and service pod scaling. Off until the Kubernetes client is agreed with DevOps.</summary>
    public bool BrokerLifecycleEnabled { get; set; }

    /// <summary>Partitions submitted to Kafka per reassignment wave. The next wave starts when the previous one finishes.</summary>
    public int ReassignmentBatchPartitions { get; set; } = 100;

    public bool AllowTopicMigration { get; set; }

    public int MigrationBackupRetentionHours { get; set; } = 168;

    public int MigrationMaxBackupMinutes { get; set; } = 15;

    public int MigrationHoldMinutes { get; set; } = 15;

    public long MigrationUnknownDiskMaxBytes { get; set; } = 1_073_741_824;

    public bool TopicOperatorManagesLinkTopics { get; set; }

    public string Workloads { get; set; } = "";

    public string WorkloadNamespace { get; set; } = "";

    public bool ManualChecklist { get; set; }
}

public sealed class KafkaOpsConnectionOptions
{
    public const string SectionName = "KafkaOps:Connection";

    public string BootstrapServers { get; set; } = "";

    public bool SaslProtocolEnabled { get; set; }

    /// <summary>Same meaning as KafkaConnection:Protocol. Empty uses the shared connection's value.</summary>
    public Confluent.Kafka.SecurityProtocol? Protocol { get; set; }

    /// <summary>Same meaning as KafkaConnection:Mechanism. Empty uses the shared connection's value.</summary>
    public Confluent.Kafka.SaslMechanism? Mechanism { get; set; }

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }
}
