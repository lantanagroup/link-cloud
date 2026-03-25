using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Adapts <see cref="ITestOutputHelper"/> to write directly to <see cref="Console"/>.
/// This test is designed to be run from a console or CI pipeline, so real-time
/// Console output is preferred over xUnit's buffered test output.
/// Can be updated to write to both if needed in the future.
/// </summary>
public class DualOutputHelper : ITestOutputHelper
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
