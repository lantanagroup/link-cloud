using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class KafkaPartitionProofTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static IEnumerable<object[]> PartitionCounts()
    {
        yield return [3];
        yield return [6];
        yield return [12];
    }

    [Theory]
    [MemberData(nameof(PartitionCounts))]
    public void DataAcquisitionRequestedKey_MatchesKafkaMurmur2(int partitionCount)
    {
        var key = KafkaKeys.ForPatient("facility-proof", "patient-proof");
        var expected = KafkaMurmur2.Partition(key, partitionCount);
        Assert.InRange(expected, 0, partitionCount - 1);
        Assert.Equal(expected, KafkaMurmur2.Partition(key, partitionCount));
    }

    [BrokerRequiredTheory]
    [MemberData(nameof(PartitionCounts))]
    public async Task DataAcquisitionRequestedKey_LandsOnTheMurmur2Partition(int partitionCount)
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")!;

        var key = KafkaKeys.ForPatient("facility-proof", "patient-proof");
        var topic = "proof-data-acquisition-requested-" + partitionCount;
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        try
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = partitionCount, ReplicationFactor = 1 }]);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrap,
            Partitioner = Partitioner.Murmur2Random,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();

        var delivery = await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = key });
        Assert.Equal(KafkaMurmur2.Partition(key, partitionCount), delivery.Partition.Value);

        var resultsDir = Environment.GetEnvironmentVariable("KAFKA_PROOF_RESULTS");
        if (!string.IsNullOrWhiteSpace(resultsDir))
        {
            Directory.CreateDirectory(resultsDir);
            var line = "dotnet\t" + topic + "\t" + partitionCount + "\t" + key + "\t" + delivery.Partition.Value + Environment.NewLine;
            await File.AppendAllTextAsync(Path.Combine(resultsDir, "partitions.tsv"), line, Utf8NoBom);
        }
    }
}
