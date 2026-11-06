using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Extensions;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
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
        Assert.IsType<RetentionCheck>(scope.ServiceProvider.GetRequiredService<IRetentionCheckWorker>());
        Assert.IsType<NoRetentionResends>(scope.ServiceProvider.GetRequiredService<IRetentionPendingResender>());
    }

    [Fact]
    public void AddRetentionSchedule_Called_RegistersTheRetentionCheckAndItsPublisher()
    {
        // Checked by descriptor: resolving them needs the database and Kafka, which Program.cs registers.
        var services = new ServiceCollection();

        services.AddRetentionSchedule(new ConfigurationBuilder().Build());

        Assert.Contains(services, d => d.ServiceType == typeof(IRetentionCheckWorker) &&
                                       d.ImplementationType == typeof(RetentionCheck) &&
                                       d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(IRetentionCheckRequestedPublisher) &&
                                       d.ImplementationType == typeof(RetentionCheckRequestedProducer) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(IProducer<string, RetentionCheckRequestedValue>) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
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

    [Fact]
    public async Task AddRetentionSchedule_Configured_RegistersRetentionCheckSettings()
    {
        await using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Retention:Window"] = "P1Y",
            ["Retention:MaxReportsPerCleanup"] = "10"
        });

        var settings = provider.GetRequiredService<RetentionCheckSettings>();

        Assert.True(settings.IsValid);
        Assert.Equal(12, settings.Window!.Count);
        Assert.Equal(10, settings.MaxReportsPerCleanup);
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

        // What Program.cs registers for the retention check, faked so no database or broker is needed.
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => Mock.Of<IRetentionEligibilityQuery>());
        services.AddScoped(_ => Mock.Of<IRetentionCleanupManager>());
        services.AddSingleton(Mock.Of<IProducer<string, RetentionCheckRequestedValue>>());

        services.AddRetentionSchedule(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
