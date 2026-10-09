namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public static class KafkaBrowseLimits
{
    public const string GroupId = "link-kafka-ops-browse";
    public const int DefaultLimit = 25;
    public const int MaxLimit = 50;
    public const int MaxPageBytes = 1_048_576;
    public const int MaxPartitions = 64;
    public const int MaxScanned = 2_000;
    public const int ScanBatch = 200;
    public const int MaxScanBatch = 500;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
}

public sealed class KafkaBrowsePage
{
    public string Topic { get; set; } = "";
    public string Mode { get; set; } = "";
    public List<KafkaBrowseRecord> Records { get; set; } = [];
    public KafkaBrowseMetadata Metadata { get; set; } = new();
}

public sealed class KafkaBrowseMetadata
{
    public int Returned { get; set; }
    public int Total { get; set; }
    public bool Truncated { get; set; }
    public bool CapHit { get; set; }
    public int Scanned { get; set; }
    public bool More { get; set; }
    public string Resume { get; set; } = "";
    public long ElapsedMs { get; set; }
}

public sealed class KafkaBrowseRecord
{
    public int Partition { get; set; }
    public long Offset { get; set; }
    public long TimestampUnixMs { get; set; }
    public string? Key { get; set; }
    public string? KeyNote { get; set; }
    public string? Value { get; set; }
    public string? ValueSummary { get; set; }
    public string? ValuePretty { get; set; }
    public bool Truncated { get; set; }
    public int ByteSize { get; set; }
    public List<KafkaBrowseHeader> Headers { get; set; } = [];
    public KafkaBrowseLink Link { get; set; } = new();
}

public sealed class KafkaBrowseHeader
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class KafkaBrowseLink
{
    public string? FacilityId { get; set; }
    public string? PatientId { get; set; }
    public string? ReportId { get; set; }
    public string? AdhocReportId { get; set; }
    public string? CorrelationId { get; set; }
    public string? RetryCount { get; set; }
    public string? ExceptionService { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? RetryExceptionMessage { get; set; }
    public string? ExceptionFacilityId { get; set; }
    public string? ExceptionPartition { get; set; }
    public string? ExceptionOffset { get; set; }
    public List<KafkaBrowseHeader> ExceptionHeaders { get; set; } = [];
}

public sealed class KafkaNamedTopic
{
    public string Topic { get; set; } = "";
    public string Main { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool Exists { get; set; }
    public int Partitions { get; set; }
    public long HighWatermarkSum { get; set; }
    public long Lag { get; set; }
    public bool LagKnown { get; set; } = true;
    public bool Browsable { get; set; }
}

public sealed class KafkaFamilyView
{
    public string Main { get; set; } = "";
    public List<KafkaNamedTopic> Members { get; set; } = [];
    public string? Error { get; set; }
}
