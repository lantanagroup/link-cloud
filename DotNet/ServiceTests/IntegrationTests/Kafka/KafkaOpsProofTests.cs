using Task = System.Threading.Tasks.Task;
using Confluent.Kafka;

namespace IntegrationTests.Kafka;

[Trait("Category", "IntegrationTests")]
public class KafkaOpsProofTests
{
    [BrokerRequiredFact]
    public void Overview_ReadsTheClusterWhenBootstrapIsSet()
    {
        var bootstrap = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP");
        if (string.IsNullOrWhiteSpace(bootstrap))
            throw new InvalidOperationException("KAFKA_BOOTSTRAP is not set.");

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        var metadata = admin.GetMetadata(TimeSpan.FromSeconds(20));
        Assert.NotEmpty(metadata.Brokers);
    }

    private sealed class BrokerRequiredFactAttribute : FactAttribute
    {
        public BrokerRequiredFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
                Skip = "KAFKA_BOOTSTRAP is not set.";
        }
    }
}
