using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

/// <summary>
/// Adapts <see cref="ITestOutputHelper"/> to write output appropriately for the environment.
/// Uses Console.WriteLine for local development and ITestOutputHelper for CI builds.
/// </summary>
public class DualOutputHelper : ITestOutputHelper
{
    private readonly ITestOutputHelper? _inner;
    private readonly bool _isCiEnvironment;

    public DualOutputHelper(ITestOutputHelper? inner = null)
    {
        _inner = inner;
        _isCiEnvironment = IsCiEnvironment();
    }

    public void WriteLine(string message)
    {
        if (_isCiEnvironment && _inner != null)
        {
            try
            {
                _inner.WriteLine(message);
            }
            catch (InvalidOperationException)
            {
                // ITestOutputHelper throws if called after the test has completed
                // Fall back to console output
                Console.WriteLine(message);
            }
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    public void WriteLine(string format, params object[] args)
    {
        if (_isCiEnvironment && _inner != null)
        {
            try
            {
                _inner.WriteLine(format, args);
            }
            catch (InvalidOperationException)
            {
                // Fall back to console output
                Console.WriteLine(format, args);
            }
        }
        else
        {
            Console.WriteLine(format, args);
        }
    }

    private static bool IsCiEnvironment()
    {
        // Check for common CI environment variables
        var ciIndicators = new[]
        {
            "CI",                    // General CI indicator
            "CONTINUOUS_INTEGRATION", // Some CI systems
            "BUILD_NUMBER",          // Azure DevOps, Jenkins
            "BUILD_ID",              // Jenkins, GitLab
            "GITHUB_ACTIONS",        // GitHub Actions
            "GITLAB_CI",             // GitLab CI
            "JENKINS_HOME",          // Jenkins
            "TEAMCITY_VERSION",      // TeamCity
            "TF_BUILD",              // Azure DevOps
            "CIRCLECI",              // CircleCI
            "TRAVIS",                // Travis CI
            "APPVEYOR",              // AppVeyor
        };

        return ciIndicators.Any(indicator =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(indicator)));
    }
}
