using Task = System.Threading.Tasks.Task;
using Confluent.Kafka;

namespace IntegrationTests.Kafka;

[Trait("Category", "IntegrationTests")]
public class KafkaOpsProofTests
{
    [Fact]
    public void Overview_ReadsTheClusterWhenBootstrapIsSet()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
            return;

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        var metadata = admin.GetMetadata(TimeSpan.FromSeconds(20));
        Assert.NotEmpty(metadata.Brokers);
    }
}
