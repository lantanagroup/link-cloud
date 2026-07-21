using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Shared xUnit fixture for BackendE2ETests.
/// Builds a DI container with all automation services registered.
/// Tests resolve services directly from <see cref="ServiceProvider"/>.
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
        builder.Services.AddSingleton<ConsoleAutomationOutput>();
        builder.Services.AddSingleton<IAutomationOutput>(sp => sp.GetRequiredService<ConsoleAutomationOutput>());

        // Infrastructure
        builder.Services.AddSingleton(sp => new LantanaGroup.Link.Automation.Link.Helpers.DatabaseConnectionFactory(sp.GetRequiredService<AutomationConfig>().Database));

        // ServiceRegistry — point each service directly at its host
        builder.Services.Configure<ServiceRegistry>(opts =>
        {
            opts.TenantService = new TenantServiceRegistration { TenantServiceUrl = TestConfig.TenantServiceBase };
            opts.CensusServiceUrl = TestConfig.CensusServiceBase;
            opts.DataAcquisitionServiceUrl = TestConfig.DataAcquisitionServiceBase;
            opts.NormalizationServiceUrl = TestConfig.NormalizationServiceBase;
            opts.QueryDispatchServiceUrl = TestConfig.QueryDispatchServiceBase;
            opts.ReportServiceUrl = TestConfig.ReportServiceBase;
            opts.MeasureServiceUrl = TestConfig.MeasureServiceBase;
            opts.ValidationServiceUrl = TestConfig.ValidationServiceBase;
            opts.SubmissionServiceUrl = TestConfig.SubmissionServiceBase;
        });

        // Auth — E2E tests use anonymous
        builder.Services.Configure<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>(opts =>
        {
            opts.AllowAnonymous = true;
        });
        builder.Services.Configure<LinkTokenServiceSettings>(_ => { });
        builder.Services.AddSingleton<ICreateSystemToken, NoOpSystemToken>();

        // LinkSdk clients (IFacilityServiceClient, IReportServiceClient, etc.)
        builder.Services.AddLinkSdk();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpClient<LokiScraper>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<AutomationConfig>();
            var configuredBaseUrl = string.IsNullOrWhiteSpace(cfg.LokiBaseUrl)
                ? "http://localhost:3100"
                : cfg.LokiBaseUrl;

            if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri))
                client.BaseAddress = baseUri;
        });

        // Automation infrastructure
        builder.Services.AddSingleton(sp => {
            var cfg = sp.GetRequiredService<AutomationConfig>();
            return new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);
        });
        builder.Services.AddSingleton<PipelineDataReader>();

        // Helpers
        builder.Services.AddTransient<ValidationApiHelper>();
        builder.Services.AddTransient<ReportApiHelper>();

        // Validators
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
