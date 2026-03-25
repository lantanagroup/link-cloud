using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Adapts <see cref="ITestOutputHelper"/> to write directly to <see cref="Console"/>.
/// The CI workflow uses --logger "console;verbosity=detailed" which captures
/// Console.WriteLine output, so ITestOutputHelper is not needed and would
/// cause duplicate lines.
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
