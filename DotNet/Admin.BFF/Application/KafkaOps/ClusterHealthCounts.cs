namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public readonly record struct ClusterHealthCounts(int UnderReplicated, int Offline, int IsrShrunk)
{
    public static ClusterHealthCounts ForPartition(int leader, IReadOnlyList<int> replicas, IReadOnlyList<int> isr)
    {
        var offline = leader < 0 ? 1 : 0;
        var under = replicas.Count > 0 && isr.Count < replicas.Count ? 1 : 0;
        var shrunk = leader >= 0 && isr.Contains(leader) && isr.Count < replicas.Count ? 1 : 0;
        return new ClusterHealthCounts(under, offline, shrunk);
    }
}
