using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

public class RunAutomationOutput(Action<string> onWriteLine) : IAutomationOutput
{
    public void WriteLine(string message)
    {
        onWriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        onWriteLine(string.Format(format, args));
    }
}
