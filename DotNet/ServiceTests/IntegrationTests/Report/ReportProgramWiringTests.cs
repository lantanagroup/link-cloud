using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationTests.Report;

/// <summary>
/// Runs Report's real Program.cs far enough to see what it registers, so a retention registration lost in a
/// merge, or moved behind RetryScheduleService, fails here instead of at startup.
/// </summary>
/// <remarks>
/// The host is stopped as soon as the service collection is complete, before anything is built or started,
/// so nothing connects to SQL Server, Kafka or App Configuration.
/// </remarks>
[Trait("Category", "IntegrationTests")]
public class ReportProgramWiringTests
{
    [Fact]
    public void Program_Registers_RetentionWorkerAndReconciler()
    {
        var services = CaptureReportServices();

        Assert.Contains(services, d => d.ServiceType == typeof(IRetentionCheckWorker));
        Assert.Contains(services, d => d.ServiceType == typeof(IRetentionScheduleReconciler));
    }

    [Fact]
    public void Program_Registers_RetentionHostedServiceBeforeRetryScheduleService()
    {
        var hostedServices = CaptureReportServices()
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType)
            .ToList();

        var retention = hostedServices.IndexOf(typeof(RetentionScheduleHostedService));
        var retry = hostedServices.IndexOf(typeof(RetryScheduleService));

        Assert.True(retention >= 0, "RetentionScheduleHostedService is not registered.");
        Assert.True(retry >= 0, "RetryScheduleService is not registered.");
        Assert.True(retention < retry,
                    "RetentionScheduleHostedService must be registered before RetryScheduleService.");
    }

    private static IReadOnlyList<ServiceDescriptor> CaptureReportServices()
    {
        IReadOnlyList<ServiceDescriptor>? captured = null;

        // Any public Report type locates the entry point; Report's top-level Program is not visible here.
        // Pinned rather than taken from the machine running the tests: under Production, Report's startup
        // requires Redis for its pipeline abort registry and throws before the services can be captured.
        using var factory = new WebApplicationFactory<RetentionCheckJob>()
            .WithWebHostBuilder(builder => builder
                .UseEnvironment(Environments.Development)
                .ConfigureTestServices(services =>
                {
                    captured = services.ToList();
                    throw new ServicesCapturedException();
                }));

        var exception = Record.Exception(() => factory.Services);

        // Anything other than our own stop means Program.cs failed first; show that failure, not a null.
        if (exception is not ServicesCapturedException)
        {
            throw new InvalidOperationException(
                "Report's Program.cs failed before its services could be captured.",
                exception);
        }

        Assert.NotNull(captured);

        return captured;
    }

    private sealed class ServicesCapturedException : Exception
    {
    }
}
