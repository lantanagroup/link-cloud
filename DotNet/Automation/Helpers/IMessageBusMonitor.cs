namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Generic interface for monitoring a message bus (e.g., Kafka, RabbitMQ, Azure Service Bus)
/// for error/retry messages during an automation run.
/// </summary>
public interface IMessageBusMonitor : IAsyncDisposable
{
    /// <summary>
    /// Initialize and start monitoring. Safe to call multiple times.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// All captured error/retry messages.
    /// </summary>
    IReadOnlyList<string> CapturedErrors { get; }

    /// <summary>
    /// Whether any errors have been captured.
    /// </summary>
    bool HasErrors { get; }
}
