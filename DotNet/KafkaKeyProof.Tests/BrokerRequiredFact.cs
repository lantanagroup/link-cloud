using Xunit;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

public sealed class BrokerRequiredFactAttribute : FactAttribute
{
    public BrokerRequiredFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
            Skip = "KAFKA_BOOTSTRAP is not set.";
    }
}

public sealed class BrokerRequiredTheoryAttribute : TheoryAttribute
{
    public BrokerRequiredTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
            Skip = "KAFKA_BOOTSTRAP is not set.";
    }
}
