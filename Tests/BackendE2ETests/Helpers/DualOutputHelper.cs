using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Wraps <see cref="ITestOutputHelper"/> to write to both the xUnit test output
/// (captured for test results) and <see cref="Console"/> (visible in real-time
/// during dotnet test execution). This works around xUnit's buffering behavior
/// where ITestOutputHelper output only appears after the test completes.
/// </summary>
public class DualOutputHelper(ITestOutputHelper inner) : ITestOutputHelper
{
    public void WriteLine(string message)
    {
        Console.WriteLine(message);
        try
        {
            inner.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // ITestOutputHelper throws if called after the test has completed
            // (e.g., during async disposal). Console.WriteLine already captured it.
        }
    }

    public void WriteLine(string format, params object[] args)
    {
        Console.WriteLine(format, args);
        try
        {
            inner.WriteLine(format, args);
        }
        catch (InvalidOperationException)
        {
            // Same guard as above
        }
    }
}
