using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.DependencyInjection;
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

        var sdkSettings = new ApiClientSettings
        {
            BaseUrl = automationCfg.AdminBffBase,
            BearerToken = automationCfg.AdminBffOAuth.ShouldAuthenticate
                ? AuthHelper.GetBearerToken(automationCfg.AdminBffOAuth)
                : null
        };
        builder.Services.AddLinkSdk(sdkSettings);

        builder.Services.AddSingleton(sp => new LokiScraper(sp.GetRequiredService<IAutomationOutput>(), sp.GetRequiredService<AutomationConfig>()));
        builder.Services.AddSingleton(sp => new FhirDataLoader(sp.GetRequiredService<AutomationConfig>().ExternalFhirServerBase, sp.GetRequiredService<AutomationConfig>()));
        builder.Services.AddSingleton<PipelineDataReader>();

        // API clients (transient — lightweight wrappers)
        builder.Services.AddTransient<FacilityApiClient>();
        builder.Services.AddTransient<NormalizationApiClient>();
        builder.Services.AddTransient<QueryConfigApiClient>();
        builder.Services.AddTransient<ValidationApiClient>();

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
}
