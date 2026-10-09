using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public class MurmurVectorTests
{
    [Theory]
    [InlineData("", 275646681, 6, 3)]
    [InlineData("a", -1563381124, 6, 4)]
    [InlineData("21", -973932308, 6, 0)]
    [InlineData("kafka", -798503068, 6, 4)]
    [InlineData("facility", -1544110641, 6, 5)]
    [InlineData("{\"facilityId\":\"abc\",\"patientId\":\"p1\"}", 1649629686, 6, 0)]
    public void SharedPartitioner_MatchesJavaMurmur2Vectors(string key, int hash, int partitions, int partition)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        Assert.Equal(hash, KafkaMurmur.Murmur2(bytes));
        Assert.Equal(hash, KafkaMurmur2.Murmur2(bytes));
        Assert.Equal(partition, KafkaMurmur.Partition(key, partitions));
        Assert.Equal(partition, KafkaMurmur2.Partition(key, partitions));
        Assert.Equal(KafkaMurmur.ToPositive(hash) % partitions, partition);
    }

    [Theory]
    [InlineData(0, 6, 0)]
    [InlineData(1, 6, 1)]
    [InlineData(5, 6, 5)]
    [InlineData(7, 3, 1)]
    public void NullKey_UsesSourcePartitionModulo(int source, int partitions, int expected)
    {
        Assert.Equal(expected, KafkaMurmur.NullKeyPartition(source, partitions));
    }
}
