using Confluent.Kafka;

namespace LantanaGroup.Link.Shared.Application.Models.Configs;

public class KafkaConnection
{
    public ConsumerConfig CreateConsumerConfig()
    {
        return new ConsumerConfig
        {
            BootstrapServers = string.Join(", ", BootstrapServers),
            ClientId = ClientId,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = true
        };
    }

    public ProducerConfig CreateProducerConfig()
    {
        return new ProducerConfig
        {
            BootstrapServers = string.Join(", ", BootstrapServers),
            ClientId = ClientId
        };
    }

    public List<string> BootstrapServers { get; set; } = new List<string>();
    public string ClientId { get; set; } = string.Empty;
    public string GroupId { get; set; } = "default";    
}
