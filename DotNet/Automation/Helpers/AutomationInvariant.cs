namespace LantanaGroup.Automation.Helpers;

public static class AutomationInvariant
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
