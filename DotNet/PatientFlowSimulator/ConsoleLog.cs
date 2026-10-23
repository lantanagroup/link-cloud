namespace LantanaGroup.Link.PatientFlowSimulator;

/// <summary>
/// Timestamped console output. The simulator is a tool, not a service, so it does not use Serilog.
/// </summary>
public static class ConsoleLog
{
    /// <summary>
    /// Writes one line prefixed with the UTC time.
    /// </summary>
    public static void Write(string message)
        => Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {message}");
}
