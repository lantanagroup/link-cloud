using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LantanaGroup.Link.Report.Application.Extensions;

/// <summary>
/// Registers the daily retention schedule: its settings, the retention participants, the retention worker, the
/// reconciler and the startup hook that runs it.
/// </summary>
public static class RetentionScheduleRegistration
{
    /// <summary>
    /// Adds the retention schedule. Call it before RetryScheduleService is registered: hosted services start
    /// in registration order, and the retention job must be settled before the scheduler starts.
    /// </summary>
    /// <remarks>
    /// Retention:Participants, Retention:Window and Retention:MaxReportsPerCleanup are read here, once. A change
    /// takes effect when Report restarts and never alters a cleanup that already exists.
    /// </remarks>
    public static IServiceCollection AddRetentionSchedule(this IServiceCollection services,
                                                          IConfiguration configuration)
    {
        services.Configure<RetentionScheduleSettings>(configuration.GetSection(RetentionScheduleSettings.Key));
        services.TryAddSingleton(RetentionParticipants.FromConfiguration(configuration));
        services.TryAddSingleton(RetentionCheckSettings.FromConfiguration(configuration));

        services.AddScoped<IRetentionCheckWorker, LoggingRetentionCheck>();
        services.TryAddScoped<IRetentionPendingResender, NoRetentionResends>();
        services.AddSingleton<IRetentionScheduleReconciler, RetentionScheduleReconciler>();
        services.AddHostedService<RetentionScheduleHostedService>();

        return services;
    }
}
