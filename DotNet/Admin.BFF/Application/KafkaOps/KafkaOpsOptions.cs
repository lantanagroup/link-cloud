namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class KafkaOpsOptions
{
    public const string SectionName = "KafkaOps";

    public int MaxPartitionsPerTopic { get; set; } = 24;

    public bool RequireSecondApprover { get; set; }

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

    public string ComposeProject { get; set; } = "";

    public string KubernetesNamespace { get; set; } = "kafka";

    public string KafkaNodePool { get; set; } = "";

    public string ConsumerDeployments { get; set; } = "";

    public string BrokerService { get; set; } = "broker-0";

    public string ConsumerService { get; set; } = "consumer";

    public int ReassignmentThrottleBytesPerSecond { get; set; }
}
