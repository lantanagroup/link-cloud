using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Models;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Link.UI.Tests;

public class LiveMonitorRenderTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var content = UiRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = "Link.UI",
            ContentRootPath = content,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(Link.UI.Controllers.HomeController).Assembly)
            .AddApplicationPart(typeof(LiveMonitorProbeController).Assembly);
        builder.Services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Add("/Views/Automation/{0}.cshtml");
        });
        builder.Services.AddSingleton<IAdminBffUserService, SilentUserService>();
        builder.Services.Configure<LinkUiFeatureOptions>(options => options.AutomationEnabled = true);

        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapControllers();
        await _app.StartAsync();

        var address = _app.Urls.Single();
        var port = new Uri(address).Port;
        if (port is >= 5280 and <= 5292)
        {
            await _app.StopAsync();
            throw new InvalidOperationException("The render host bound a reserved port.");
        }

        _client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Live_run_renders_the_monitor_and_a_finished_run_does_not()
    {
        var live = await ReadAsync("/probe/runs/live");
        live.Should().Contain("id=\"liveUtilizationCard\"");
        live.Should().Contain("live-utilization.js");
        live.Should().Contain("data-health-url");

        var finished = await ReadAsync("/probe/runs/finished");
        finished.Should().NotContain("id=\"liveUtilizationCard\"");
        finished.Should().NotContain("live-utilization.js");
    }

    private async Task<string> ReadAsync(string path)
    {
        var response = await _client!.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(((int)response.StatusCode) + " " + path + " " + body);
        return body;
    }

    private static string UiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private sealed class SilentUserService : IAdminBffUserService
    {
        public Task<AdminBffUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AdminBffUser?>(null);
    }
}

public sealed class LiveMonitorProbeController : Controller
{
    [HttpGet("/probe/runs/{kind}")]
    public IActionResult Show(string kind)
    {
        var live = !string.Equals(kind, "finished", StringComparison.OrdinalIgnoreCase);
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var started = new DateTimeOffset(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);
        var page = new AutomationRunPage
        {
            Found = true,
            LiveConfigured = true,
            RequestedId = id,
            Run = AutomationRules.ToRow(
                id,
                "Sample",
                "Custom",
                live ? "Running" : "Succeeded",
                1,
                1,
                false,
                started,
                started,
                live ? null : started.AddMinutes(1),
                null,
                null,
                "not-a-facility",
                false,
                null,
                null),
            Detail = new AutomationRunSummary
            {
                RunId = id,
                RunName = "Sample",
                Status = live ? AutomationRunStatus.Running : AutomationRunStatus.Succeeded,
                CreatedAt = started,
                StartedAt = started,
                FinishedAt = live ? null : started.AddMinutes(1),
                FacilityId = "not-a-facility",
                ReportId = "report-1"
            }
        };
        return View("/Views/Automation/Run.cshtml", page);
    }
}
