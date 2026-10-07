using System.Net;
using FluentAssertions;
using Link.UI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Link.UI.Tests;

public class AutomationFeatureGateTests
{
    [Fact]
    public async Task Disabled_automation_is_absent_from_the_shell_and_its_routes()
    {
        await using var factory = new GateHost();
        factory.Services.GetRequiredService<IOptions<LinkUiFeatureOptions>>().Value.AutomationEnabled.Should().BeFalse();
        var engine = factory.Services.GetService<LinkAutomationEngineStatus>();
        if (engine is not null)
            engine.Ready.Should().BeFalse();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var home = await client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await home.Content.ReadAsStringAsync();
        html.Should().NotContain("bi-lightning-charge");
        html.Should().NotContain("Active runs");
        html.Should().NotContain("Open the runs dashboard");

        foreach (var path in new[]
        {
            "/Automation",
            "/Cleanup",
            "/Metrics",
            "/ApiHealth",
            "/Runs",
            "/api/runs",
            "/api/api-health-runs",
            "/hubs/runs",
            "/hubs/cleanup"
        })
        {
            AutomationSurface.IsAutomationPath(path).Should().BeTrue(path);
            var response = await client.GetAsync(path);
            ((int)response.StatusCode).Should().Be(404, path);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Tenants")]
    [InlineData("/health")]
    [InlineData("/api/login")]
    public void Ordinary_paths_stay_open(string path)
    {
        AutomationSurface.IsAutomationPath(path).Should().BeFalse();
    }

    private sealed class GateHost : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(WebHostDefaults.ContentRootKey, FindContentRoot());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDB:ConnectionString"] = "not-a-connection-string",
                    ["MongoDB:DatabaseName"] = "link-ui-split-gate-tests",
                    ["LinkUi:AutomationEnabled"] = "false",
                    ["Authentication:EnableAnonymousAccess"] = "true",
                    ["Authentication:RequireBffSession"] = "false",
                    ["Authentication:ApiBearer:Enabled"] = "false",
                    ["ExternalConfigurationSource"] = "",
                    ["Telemetry:EnableTelemetry"] = "false"
                });
            });
        }

        private static string FindContentRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var sibling = Path.Combine(dir.FullName, "Link.UI", "Link.UI.csproj");
                if (File.Exists(sibling))
                    return Path.Combine(dir.FullName, "Link.UI");
                var nested = Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj");
                if (File.Exists(nested))
                    return Path.Combine(dir.FullName, "DotNet", "Link.UI");
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Link.UI content root was not found.");
        }
    }
}
