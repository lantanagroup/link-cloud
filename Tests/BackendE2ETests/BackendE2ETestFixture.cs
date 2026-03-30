using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestSharp;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Shared xUnit fixture for BackendE2ETests.
/// Builds a proper DI container with all automation services registered,
/// following the same Host.CreateApplicationBuilder() pattern used by
/// the ServiceTests integration test fixtures.
/// </summary>
public sealed class BackendE2ETestFixture : IDisposable
{
    private readonly IHost _host;
    public IServiceProvider ServiceProvider { get; }

    public BackendE2ETestFixture()
    {
        var automationCfg = TestConfig.BuildAutomationConfig();

        var builder = Host.CreateApplicationBuilder();

        // Core configuration
        builder.Services.AddSingleton(automationCfg);

        // Output helper (console-based for CI)
        builder.Services.AddSingleton<DualOutputHelper>();
        builder.Services.AddSingleton<IAutomationOutput>(sp => sp.GetRequiredService<DualOutputHelper>());

        // Infrastructure
        builder.Services.AddSingleton(sp => new DatabaseConnectionFactory(sp.GetRequiredService<AutomationConfig>().Database));
        builder.Services.AddSingleton(sp => AdminBffClientFactory.Create(sp.GetRequiredService<AutomationConfig>()));

        // ServiceRegistry — point all services at AdminBFF base for E2E tests
        // The AdminBFF YARP proxy routes api/census/*, api/data/*, etc. to the real services.
        // ServiceRegistry expects raw base URLs (without /api) since *ApiUrl properties append /api.
        var bffBase = automationCfg.AdminBffBase.TrimEnd('/');
        if (bffBase.EndsWith("/api")) bffBase = bffBase[..^4];

        builder.Services.Configure<ServiceRegistry>(opts =>
        {
            opts.TenantService = new TenantServiceRegistration { TenantServiceUrl = bffBase };
            opts.CensusServiceUrl = bffBase;
            opts.DataAcquisitionServiceUrl = bffBase;
            opts.NormalizationServiceUrl = bffBase;
            opts.ReportServiceUrl = bffBase;
            opts.MeasureServiceUrl = bffBase;
            opts.ValidationServiceUrl = bffBase;
            opts.SubmissionServiceUrl = bffBase;
        });

        // Auth — E2E tests use anonymous or OAuth token via AdminBFF
        builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
        {
            opts.AllowAnonymous = true;
        });
        builder.Services.Configure<LinkTokenServiceSettings>(_ => { });
        builder.Services.AddSingleton<ICreateSystemToken, NoOpSystemToken>();

        builder.Services.AddLinkSdk();

        builder.Services.AddSingleton(sp => new LokiScraper(sp.GetRequiredService<IAutomationOutput>(), sp.GetRequiredService<AutomationConfig>()));
        builder.Services.AddSingleton(sp => new FhirDataLoader(sp.GetRequiredService<AutomationConfig>().ExternalFhirServerBase, sp.GetRequiredService<AutomationConfig>()));
        builder.Services.AddSingleton<PipelineDataReader>();

        // Helpers
        builder.Services.AddTransient<ValidationApiHelper>();

        // Validators (transient)
        builder.Services.AddTransient<ReportDatabaseValidator>();
        builder.Services.AddTransient<ReportAbsManifestValidator>();
        builder.Services.AddTransient<DataAcquisitionDatabaseValidator>();
        builder.Services.AddTransient<NormalizationDatabaseValidator>();
        builder.Services.AddTransient<TenantDatabaseValidator>();
        builder.Services.AddTransient<ValidationResultsValidator>();

        // Snapshot / diagnostics
        builder.Services.AddTransient<PipelineSnapshot>();

        _host = builder.Build();
        _host.StartAsync().GetAwaiter().GetResult();
        ServiceProvider = _host.Services;
    }

    public TestServices GetTestServices() => new(ServiceProvider);

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }

    /// <summary>
    /// No-op token service for E2E tests where auth is anonymous.
    /// </summary>
    private sealed class NoOpSystemToken : ICreateSystemToken
    {
        public Task<string> ExecuteAsync(string key, int timespan) => Task.FromResult(string.Empty);
    }
}
