namespace Automation.UI.Models;

public sealed class PatientStateEvent
{
    public Guid EventId { get; init; }
    public Guid RunId { get; init; }
    public string PatientId { get; init; } = "";
    public PatientEventType EventType { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string? Source { get; init; }
    public string? Notes { get; init; }
}
