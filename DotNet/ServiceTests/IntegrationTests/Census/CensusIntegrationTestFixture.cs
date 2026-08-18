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
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using Quartz;
using Quartz.Logging;
// Added for SetupServiceInformation
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Census;

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

        var builder = Host.CreateApplicationBuilder();

        var assemblyVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0-test";

        // Register ServiceInformation using the extension method with the in-memory db name
        var serviceInformation = builder.SetupServiceInformation(
            "CensusService", // Replace with a constant if available
            assemblyVersion
        );

        builder.Services.AddDbContext<CensusContext>(options =>
        {
            options.UseInMemoryDatabase(dbName);
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });

        // Core services
        builder.Services.AddScoped<IPatientEventManager, PatientEventManager>();
        builder.Services.AddScoped<IPatientEventQueries, PatientEventQueries>();
        builder.Services.AddScoped<IPatientEncounterManager, PatientEncounterManager>();
        builder.Services.AddScoped<IPatientEncounterQueries, PatientEncounterQueries>();
        builder.Services.AddScoped<ICensusConfigManager, CensusConfigManager>();
        builder.Services.AddScoped<IBaseEntityRepository<CensusConfigEntity>, CensusEntityRepository<CensusConfigEntity>>();
        builder.Services.AddScoped<IBaseEntityRepository<PatientEncounter>, CensusEntityRepository<PatientEncounter>>();
        builder.Services.AddScoped<IBaseEntityRepository<PatientEvent>, CensusEntityRepository<PatientEvent>>();
        builder.Services.AddScoped<IBaseEntityRepository<PatientIdentifier>, CensusEntityRepository<PatientIdentifier>>();
        builder.Services.AddScoped<IBaseEntityRepository<PatientVisitIdentifier>, CensusEntityRepository<PatientVisitIdentifier>>();
        builder.Services.AddScoped<ICensusSchedulingRepository, CensusSchedulingRepository>();
        builder.Services.AddScoped<IPatientListService, PatientListService>();
        builder.Services.AddScoped<ICernerListService, CernerListService>();

        builder.Services.AddSingleton<ICensusServiceMetrics, NullCensusServiceMetrics>();
        builder.Services.AddSingleton<ITenantApiService, NullTenantApiService>();

        builder.Services.AddQuartz(q => q.UseInMemoryStore());

        builder.Services.AddLogging(builder => builder.ClearProviders().AddProvider(new NullLoggerProvider()));

        builder.Services.AddOpenTelemetry()
                .WithTracing(b => b
                    .AddSource("CensusService")
                    .SetSampler(new AlwaysOnSampler())
                    .AddConsoleExporter());

        builder.Services.AddTransient<CensusConfigController>();
        builder.Services.AddTransient<PatientEventsController>();
        builder.Services.AddTransient<PatientEncountersController>();

        _host = builder.Build();
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