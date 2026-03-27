namespace LantanaGroup.Link.Automation.Helpers;

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
