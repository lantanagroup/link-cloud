using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LantanaGroup.Link.Report.Application.Extensions;

/// <summary>
/// Registers the daily retention schedule: its settings, the retention participants, the retention check and
/// the producer it publishes with, the reconciler and the startup hook that runs it.
/// </summary>
public static class RetentionScheduleRegistration
{
    /// <summary>
    /// Adds the retention schedule. Call it before RetryScheduleService is registered: hosted services start
    /// in registration order, and the retention job must be settled before the scheduler starts.
    /// </summary>
    /// <remarks>
    /// Retention:Participants, Retention:Window and Retention:MaxReportsPerCleanup are read here, once. A change
    /// takes effect when Report restarts and never alters a cleanup that already exists. The retention check also
    /// needs the cleanup manager, the eligibility query, the report database, a <see cref="TimeProvider"/> and
    /// a <see cref="KafkaConnection"/>, which Program.cs registers.
    /// </remarks>
    public static IServiceCollection AddRetentionSchedule(this IServiceCollection services,
                                                          IConfiguration configuration)
    {
        services.Configure<RetentionScheduleSettings>(configuration.GetSection(RetentionScheduleSettings.Key));
        services.TryAddSingleton(RetentionParticipants.FromConfiguration(configuration));
        services.TryAddSingleton(RetentionCheckSettings.FromConfiguration(configuration));

        services.AddScoped<IRetentionCheckWorker, RetentionCheck>();
        services.TryAddScoped<IRetentionPendingResender, NoRetentionResends>();
        services.TryAddSingleton<IRetentionCheckRequestedPublisher, RetentionCheckRequestedProducer>();

        // Built on first use, so a host that never runs a retention check never connects to Kafka for it.
        services.TryAddSingleton(sp =>
            new KafkaProducerFactory<string, RetentionCheckRequestedValue>(sp.GetRequiredService<KafkaConnection>())
                .CreateProducer(new ProducerConfig
                {
                    ClientId = RetentionCheckRequestedProducer.ClientId
                }));
        services.AddSingleton<IRetentionScheduleReconciler, RetentionScheduleReconciler>();
        services.AddHostedService<RetentionScheduleHostedService>();

        return services;
    }
}
