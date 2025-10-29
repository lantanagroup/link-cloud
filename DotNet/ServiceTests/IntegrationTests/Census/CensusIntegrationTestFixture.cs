using Census.Controllers;
using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Repositories;
using LantanaGroup.Link.Census.Application.Repositories.Scheduling;
using LantanaGroup.Link.Census.Application.Services;
using LantanaGroup.Link.Census.Controllers;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Census.Domain.Entities.POI;
using LantanaGroup.Link.Census.Domain.Managers;
using LantanaGroup.Link.Census.Domain.Queries;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

[CollectionDefinition("CensusIntegrationTests")]
public class DatabaseCollection : ICollectionFixture<CensusIntegrationTestFixture> { }

public sealed class CensusIntegrationTestFixture : IDisposable
{
    private IHost? _host;
    private bool _disposed;
    private IServiceScope? _scope;

    // Use a SCOPE to safely resolve scoped services
    public IServiceProvider ServiceProvider => EnsureScope().ServiceProvider;
    public CensusContext DbContext => EnsureScope().ServiceProvider.GetRequiredService<CensusContext>();

    public CensusIntegrationTestFixture()
    {
        LogProvider.SetCurrentLogProvider(NoOpLogProvider.Instance);
    }

    private IServiceScope EnsureScope()
    {
        if (_scope != null)
            return _scope;

        var host = EnsureHost();
        _scope = host.Services.CreateScope();
        return _scope;
    }

    private IHost EnsureHost()
    {
        if (_host != null)
            return _host;

        var dbName = $"CensusTestDatabase_{Guid.NewGuid():N}";

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContext<CensusContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });

                // Core services
                services.AddScoped<IPatientEventManager, PatientEventManager>();
                services.AddScoped<IPatientEventQueries, PatientEventQueries>();
                services.AddScoped<IPatientEncounterManager, PatientEncounterManager>();
                services.AddScoped<IPatientEncounterQueries, PatientEncounterQueries>();
                services.AddScoped<ICensusConfigManager, CensusConfigManager>();
                services.AddScoped<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
                services.AddScoped<IBaseEntityRepository<PatientEncounter>, CensusEntityRepository<PatientEncounter>>();
                services.AddScoped<IBaseEntityRepository<PatientEvent>, CensusEntityRepository<PatientEvent>>();
                services.AddScoped<IBaseEntityRepository<PatientIdentifier>, CensusEntityRepository<PatientIdentifier>>();
                services.AddScoped<IBaseEntityRepository<PatientVisitIdentifier>, CensusEntityRepository<PatientVisitIdentifier>>();
                services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
                services.AddScoped<IPatientListService, PatientListService>();

                services.AddSingleton<ICensusServiceMetrics, NullCensusServiceMetrics>();
                services.AddSingleton<ITenantApiService, NullTenantApiService>();

                services.AddQuartz(q => q.UseMicrosoftDependencyInjectionJobFactory());
                services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);

                services.AddLogging(builder => builder.ClearProviders().AddProvider(new NullLoggerProvider()));
                services.AddKeyedSingleton<ISchedulerFactory, StdSchedulerFactory>(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton);

                services.AddOpenTelemetry()
                        .WithTracing(b => b
                            .AddSource("CensusService")
                            .SetSampler(new AlwaysOnSampler())
                            .AddConsoleExporter());

                services.AddTransient<CensusConfigController>();
                services.AddTransient<PatientEventsController>();
                services.AddTransient<PatientEncountersController>();
            })
            .Build();

        _host.Start();
        return _host;
    }

    // New method to reset the database
    public async Task ResetDatabaseAsync()
    {
        var context = ServiceProvider.GetRequiredService<CensusContext>();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        await Task.CompletedTask; // For async compatibility
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            _scope?.Dispose();
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host?.Dispose();
        }
        finally
        {
            _disposed = true;
            _scope = null;
            _host = null;
        }
    }
}

// === NO-OP LOGGING ===
internal sealed class NullLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
    public void Dispose() { }
}

internal sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();
    public void Dispose() { }
}

internal sealed class NoOpLogProvider : ILogProvider
{
    public static readonly NoOpLogProvider Instance = new();
    private NoOpLogProvider() { }
    public Logger GetLogger(string name) => (_, __, ___, ____) => true;
    public Logger GetLogger(Type type) => GetLogger(type.FullName ?? "Unknown");
    public IDisposable OpenMappedContext(string key, object value, bool destructure = false) => NoOpDisposable.Instance;
    public IDisposable OpenNestedContext(string message) => NoOpDisposable.Instance;
}

internal sealed class NoOpDisposable : IDisposable
{
    public static readonly NoOpDisposable Instance = new();
    private NoOpDisposable() { }
    public void Dispose() { }
}

// === NULL SERVICES ===
internal class NullCensusServiceMetrics : ICensusServiceMetrics
{
    public void IncrementPatientAdmittedCounter(List<KeyValuePair<string, object?>> tags) { }
    public void IncrementPatientDischargedCounter(List<KeyValuePair<string, object?>> tags) { }
}

internal class NullTenantApiService : ITenantApiService
{
    public Task<bool> CheckFacilityExists(string facilityId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<FacilityModel> GetFacilityConfig(string facilityId, CancellationToken cancellationToken = default)
        => Task.FromResult(new FacilityModel { FacilityId = facilityId });
}