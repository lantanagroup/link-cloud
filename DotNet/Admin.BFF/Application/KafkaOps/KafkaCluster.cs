namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class ClusterSnapshot
{
    public int BrokerCount { get; set; }
    public int? ControllerId { get; set; }
    public int UnderReplicatedPartitions { get; set; }
    public int OfflinePartitions { get; set; }
    public int IsrShrunkPartitions { get; set; }
    public bool LogDirsAvailable { get; set; }
    public string LogDirDetail { get; set; } = "";
    public List<BrokerSnapshot> Brokers { get; set; } = [];
    public List<PartitionPlacement> Placements { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class BrokerSnapshot
{
    public int Id { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Rack { get; set; } = "";
    public string State { get; set; } = "";
    public int PartitionCount { get; set; }
    public int LeaderCount { get; set; }
    public long LogDirBytes { get; set; } = -1;
}

public sealed class PartitionPlacement
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public int Leader { get; set; }
    public List<int> Replicas { get; set; } = [];
    public List<int> Isr { get; set; } = [];
}

public sealed class InfraStatus
{
    public string Provider { get; set; } = "Disabled";
    public bool Enabled { get; set; }
    public string Detail { get; set; } = "";
}
