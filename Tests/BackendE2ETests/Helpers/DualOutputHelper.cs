using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Adapts <see cref="ITestOutputHelper"/> to write output appropriately for the environment.
/// Always writes to Console for visibility, and also to ITestOutputHelper when available.
/// </summary>
public class DualOutputHelper : ITestOutputHelper
{
    private readonly ITestOutputHelper? _inner;

    public DualOutputHelper(ITestOutputHelper? inner = null)
    {
        _inner = inner;
    }

    public void WriteLine(string message)
    {
        // Always write to console for visibility in CI logs
        Console.WriteLine(message);
        
        // Also write to test output helper if available (for test framework capture)
        if (_inner != null)
        {
            try
            {
                _inner.WriteLine(message);
            }
            catch (InvalidOperationException)
            {
                // ITestOutputHelper throws if called after the test has completed
                // Console output already happened above
            }
        }
    }

    public void WriteLine(string format, params object[] args)
    {
        // Always write to console for visibility in CI logs
        Console.WriteLine(format, args);
        
        // Also write to test output helper if available (for test framework capture)
        if (_inner != null)
        {
            try
            {
                _inner.WriteLine(format, args);
            }
            catch (InvalidOperationException)
            {
                // Console output already happened above
            }
        }
    }
}
