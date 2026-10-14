using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Operations.Application.KafkaOps;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Kafka;

[Trait("Category", "UnitTests")]
public class KafkaLeaderElectionTests
{
    private static ElectLeadersException Failure(params ErrorCode[] codes) => new(new ElectLeadersReport
    {
        TopicPartitions = codes.Select((code, index) => new TopicPartitionError("ReportScheduled", index, new Error(code))).ToList()
    });

    [Fact]
    public async Task ElectionNotNeeded_OnEveryPartition_IsSuccess()
    {
        await KafkaLeaderElection.RunAsync(() => throw Failure(ErrorCode.ElectionNotNeeded, ErrorCode.NoError, ErrorCode.ElectionNotNeeded));
    }

    [Fact]
    public async Task ARealElectionError_StillFails()
    {
        await Assert.ThrowsAsync<ElectLeadersException>(() => KafkaLeaderElection.RunAsync(() => throw Failure(ErrorCode.ElectionNotNeeded, ErrorCode.PreferredLeaderNotAvailable)));
    }
}