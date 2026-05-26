using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Smoke test that exercises the Automation.UI /api/runs endpoints against the
/// running docker-compose stack. Does not use <see cref="BackendE2ETestFixture"/>
/// because it talks to Automation.UI over HTTP rather than directly to Link
/// services -- the test is validating the API contract and the run lifecycle,
/// not the generation/validation internals.
///
/// Run in isolation:
///   dotnet test Tests/BackendE2ETests --filter Category=AutomationUiSmokeTest
/// </summary>
[Trait("Category", "AutomationUiSmokeTest")]
public sealed class AutomationUiApiSmokeTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;
    private const int DefaultTimeoutMinutes = 20;
    private const int MinTimeoutMinutes = 1;
    private const int MaxTimeoutMinutes = 240;

    // Seeded deterministic id from ScenarioSeedService -- same in every environment.
    private static readonly Guid AdhocReportScenarioId = new("00000000-0000-0000-0000-000000000001");

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(GetTimeoutMinutes());

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ConsoleAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();

    public AutomationUiApiSmokeTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    private static int GetTimeoutMinutes()
    {
        var configured = Environment.GetEnvironmentVariable("AUTOMATION_UI_SMOKE_TIMEOUT_MINUTES");
        if (!int.TryParse(configured, out var timeoutMinutes))
            return DefaultTimeoutMinutes;

        return timeoutMinutes is >= MinTimeoutMinutes and <= MaxTimeoutMinutes
            ? timeoutMinutes
            : DefaultTimeoutMinutes;
    }

    /// <summary>
    /// POSTs the seeded AdHoc Report [System] scenario to Automation.UI's
    /// /api/runs/start, polls /api/runs/{id}/status until the run reaches a
    /// terminal state, and asserts it succeeded.
    /// </summary>
    [Fact]
    public async Task AdHocReportScenario_CompletesSuccessfully()
    {
        using var timeoutCts = new CancellationTokenSource(Timeout);
        var cancellationToken = timeoutCts.Token;

        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookies
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(TestConfig.AutomationUiBase.TrimEnd('/') + "/") };

        // Acquire antiforgery cookie + request token from the Runs page so
        // service-to-service smoke calls can POST to endpoints protected by
        // [ValidateAntiForgeryToken].
        using var runsPageResponse = await http.GetAsync("Runs", cancellationToken);
        Assert.True(runsPageResponse.IsSuccessStatusCode,
            $"GET /Runs returned {(int)runsPageResponse.StatusCode}");

        var runsPageHtml = await runsPageResponse.Content.ReadAsStringAsync(cancellationToken);
        var requestVerificationToken = ExtractRequestVerificationToken(runsPageHtml);
        Assert.False(string.IsNullOrWhiteSpace(requestVerificationToken),
            "Could not extract __RequestVerificationToken from /Runs page.");

        http.DefaultRequestHeaders.Remove("RequestVerificationToken");
        http.DefaultRequestHeaders.Add("RequestVerificationToken", requestVerificationToken);

        // ── POST /api/runs/start ─────────────────────────────────────────
        Output.WriteLine($"Starting AdHoc Report [System] scenario ({AdhocReportScenarioId}) against {TestConfig.AutomationUiBase}");

        var startPayload = new { scenarioId = AdhocReportScenarioId, source = "AutomationUiApiSmokeTest" };
        using var startResponse = await http.PostAsJsonAsync("api/runs/start", startPayload, cancellationToken);

        Assert.True(startResponse.IsSuccessStatusCode,
            $"POST /api/runs/start returned {(int)startResponse.StatusCode}: {await startResponse.Content.ReadAsStringAsync(cancellationToken)}");

        var startBody = await startResponse.Content.ReadFromJsonAsync<StartRunResponse>(JsonOpts, cancellationToken);
        Assert.NotNull(startBody);
        Assert.NotEqual(Guid.Empty, startBody.RunId);
        Output.WriteLine($"Run started: runId={startBody.RunId} scenario={startBody.ScenarioName}");

        // ── Poll GET /api/runs/{id}/status ───────────────────────────────
        var deadline = DateTime.UtcNow.Add(Timeout);
        RunStatusResponse? statusBody = null;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, cancellationToken);

            using var statusResponse = await http.GetAsync($"api/runs/{startBody.RunId}/status", cancellationToken);
            Assert.True(statusResponse.IsSuccessStatusCode,
                $"GET /api/runs/{startBody.RunId}/status returned {(int)statusResponse.StatusCode}");

            statusBody = await statusResponse.Content.ReadFromJsonAsync<RunStatusResponse>(JsonOpts, cancellationToken);
            Assert.NotNull(statusBody);

            Output.WriteLine($"  status={statusBody.Status} isTerminal={statusBody.IsTerminal}" +
                             (statusBody.Duration != null ? $" duration={statusBody.Duration}" : ""));

            if (statusBody.IsTerminal)
                break;
        }

        Assert.NotNull(statusBody);
        Assert.True(statusBody.IsTerminal,
            $"Run {startBody.RunId} did not reach a terminal state within {Timeout.TotalMinutes} minutes.");

        if (statusBody.Status != "Succeeded")
        {
            Output.WriteLine($"Run failed. Error: {statusBody.Error}");
        }

        Assert.True(statusBody.Status == "Succeeded",
            $"Expected run to succeed but got '{statusBody.Status}'. Error: {statusBody.Error}");

        Output.WriteLine($"Run {startBody.RunId} completed successfully in {statusBody.Duration ?? "unknown duration"}.");
    }

    private static string? ExtractRequestVerificationToken(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["token"].Value) : null;
    }

    // ── Response shape mirrors AutomationRunsApiController.RunStatusResponse ──

    private sealed class StartRunResponse
    {
        public Guid RunId { get; set; }
        public Guid ScenarioId { get; set; }
        public string ScenarioName { get; set; } = string.Empty;
    }

    private sealed class RunStatusResponse
    {
        public Guid RunId { get; set; }
        public string RunName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
    }
}
