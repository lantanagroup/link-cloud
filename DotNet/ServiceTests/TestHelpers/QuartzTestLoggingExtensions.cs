using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceTests.TestHelpers;

/// <summary>
/// The logging registration any test that builds a Quartz scheduler needs.
/// </summary>
public static class QuartzTestLoggingExtensions
{
    /// <summary>
    /// Registers the shared <see cref="NullLoggerFactory"/> as the container's
    /// <see cref="ILoggerFactory"/>, for a test that creates a Quartz scheduler.
    /// </summary>
    /// <remarks>
    /// A disposable factory from <c>AddLogging()</c> is torn down when the test's container is
    /// disposed, but Quartz's logging bridge keeps a process-wide static reference to whichever
    /// factory the first scheduler in the process was built with. A later test's scheduler creation
    /// then throws <see cref="ObjectDisposedException"/> reaching through that stale reference -
    /// a failure that depends on which test ran first, so it appears and disappears with test
    /// ordering. <see cref="NullLoggerFactory.Instance"/> is a shared instance the container did not
    /// create and therefore never disposes, and its own <c>Dispose()</c> is a no-op, so it satisfies
    /// Quartz's requirement for an <see cref="ILoggerFactory"/> without that cross-test hazard.
    /// </remarks>
    public static IServiceCollection AddQuartzTestLogging(this IServiceCollection services)
    {
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        return services;
    }
}
