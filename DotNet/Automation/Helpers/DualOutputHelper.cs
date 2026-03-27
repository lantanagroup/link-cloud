namespace LantanaGroup.Link.Automation.Helpers;

/// <summary>
/// Writes automation diagnostics directly to <see cref="Console"/>.
/// </summary>
public class DualOutputHelper : IAutomationOutput
{
    public void WriteLine(string message)
    {
        Console.WriteLine(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        Console.WriteLine(format, args);
    }
}
