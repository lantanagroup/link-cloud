namespace Link.UI.Services;

/// <summary>
/// Held Kafka topics. The run engine resolves this from the host when the console
/// client is registered. A host that does not register it allows the run to start.
/// </summary>
public interface IKafkaTopicHoldSource
{
    Task<IReadOnlyList<string>> GetHeldTopicsAsync(CancellationToken cancellationToken);
}

public sealed class TopicHeldException : Exception
{
    public TopicHeldException(string topic)
        : base("Topic " + topic + " is held. The run was not started.")
    {
        Topic = topic;
    }

    public string Topic { get; }
}
