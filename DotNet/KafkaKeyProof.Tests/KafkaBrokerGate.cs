namespace LantanaGroup.Link.KafkaKeyProof.Tests;

/// <summary>
/// Skips a broker proof when <c>KAFKA_BOOTSTRAP</c> is unset.
/// xUnit 2.9 reads <see cref="FactAttribute.Skip"/> during discovery and has no runtime <c>Assert.Skip</c>.
/// </summary>
public sealed class KafkaBrokerFactAttribute : FactAttribute
{
    public KafkaBrokerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
        {
            Skip = "KAFKA_BOOTSTRAP is unset.";
        }
    }
}

/// <summary>
/// Skips a broker proof theory when <c>KAFKA_BOOTSTRAP</c> is unset.
/// </summary>
public sealed class KafkaBrokerTheoryAttribute : TheoryAttribute
{
    public KafkaBrokerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP")))
        {
            Skip = "KAFKA_BOOTSTRAP is unset.";
        }
    }
}
