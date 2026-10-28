namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Platform-agnostic output abstraction for automation diagnostics.
/// Implementations can target console, test output, event streams, etc.
/// </summary>
public interface IAutomationOutput
{
    void WriteLine(string message);
    void WriteLine(string format, params object[] args);
}

/// <summary>
/// Writes automation output directly to <see cref="Console"/>.
/// </summary>
public class ConsoleAutomationOutput : IAutomationOutput
{
    public void WriteLine(string message) => Console.WriteLine(message);
    public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
}

/// <summary>
/// Wraps an <see cref="IAutomationOutput"/> and raises an event for each log line,
/// optionally forwarding to the inner output.
/// </summary>
public sealed class EventingAutomationOutput : IAutomationOutput
{
    private readonly IAutomationOutput _inner;
    private readonly Action<string> _onLog;
    private readonly bool _forwardToInner;

    public EventingAutomationOutput(IAutomationOutput inner, Action<string> onLog, bool forwardToInner)
    {
        _inner = inner;
        _onLog = onLog;
        _forwardToInner = forwardToInner;
    }

    public void WriteLine(string message)
    {
        _onLog(message);
        if (_forwardToInner)
            _inner.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        var message = string.Format(format, args);
        WriteLine(message);
    }
}
