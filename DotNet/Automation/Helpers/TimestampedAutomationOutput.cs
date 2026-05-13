namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Wraps an <see cref="IAutomationOutput"/> and prepends UTC timestamps to every line.
/// Useful for CI environments where native timestamps are not available.
/// </summary>
public sealed class TimestampedAutomationOutput : IAutomationOutput
{
    private readonly IAutomationOutput _inner;
    private readonly string _format;

    public TimestampedAutomationOutput(IAutomationOutput inner, string format = "HH:mm:ss.fff")
    {
        _inner = inner;
        _format = format;
    }

    public void WriteLine(string message)
        => _inner.WriteLine($"[{DateTime.UtcNow.ToString(_format)}] {message}");

    public void WriteLine(string format, params object[] args)
        => WriteLine(string.Format(format, args));
}
