namespace LantanaGroup.Link.Automation.Helpers;

public interface IAutomationOutput
{
    void WriteLine(string message);
    void WriteLine(string format, params object[] args);
}

public sealed class ConsoleAutomationOutput : IAutomationOutput
{
    public void WriteLine(string message) => Console.WriteLine(message);
    public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
}
