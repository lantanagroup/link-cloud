using LantanaGroup.Link.Report.Application.Extensions;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers what <see cref="RetentionScheduleRegistration.AddRetentionSchedule"/> registers, by resolving it from
/// a validated container.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionScheduleRegistrationTests
{
    [Fact]
    public async Task AddRetentionSchedule_Called_RegistersHostedServiceWorkerAndReconciler()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>());

        Assert.Contains(provider.GetServices<IHostedService>(), s => s is RetentionScheduleHostedService);
        Assert.IsType<RetentionScheduleReconciler>(provider.GetRequiredService<IRetentionScheduleReconciler>());

        using var scope = provider.CreateScope();
        Assert.IsType<LoggingRetentionCheck>(scope.ServiceProvider.GetRequiredService<IRetentionCheckWorker>());
    }

    [Fact]
    public async Task AddRetentionSchedule_Configured_BindsRetentionScheduleSettings()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Retention:Schedule:Cron"] = "0 0 3 * * ?",
            ["Retention:Schedule:Enabled"] = "false"
        });

        var settings = provider.GetRequiredService<IOptions<RetentionScheduleSettings>>().Value;

        Assert.Equal("0 0 3 * * ?", settings.Cron);
        Assert.False(settings.Enabled);
    }

    [Fact]
    public async Task AddRetentionSchedule_Configured_RegistersRetentionParticipants()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = "Report"
        });

        var participants = provider.GetRequiredService<RetentionParticipants>();

        Assert.True(participants.IsValid);
        Assert.Equal("Report", Assert.Single(participants.Participants).ServiceName);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // The host registers Quartz itself; a unique name keeps this scheduler out of other test classes' way.
        services.AddQuartz(q =>
        {
            q.SchedulerName = $"retention-registration-tests-{Guid.NewGuid():N}";
            q.UseInMemoryStore();
        });

        services.AddRetentionSchedule(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
