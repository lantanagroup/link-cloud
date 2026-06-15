using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class ApiStabilityTest : IClassFixture<BackendE2ETestFixture>
{
    private readonly IServiceProvider _sp;
    private IAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(GetTimeoutMinutes());

    public ApiStabilityTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "ApiStabilityTest")]
    public async Task ExecuteApiStabilityTest()
    {
        using var timeoutCts = new CancellationTokenSource(Timeout);
        var cancellationToken = timeoutCts.Token;

        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookies
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(TestConfig.AutomationUiBase.TrimEnd('/') + "/")
        };

        // Antiforgery token is guaranteed on the Runs page; use it for API POSTs.
        using var runsPageResponse = await http.GetAsync("Runs", cancellationToken);
        Assert.True(runsPageResponse.IsSuccessStatusCode,
            $"GET /Runs returned {(int)runsPageResponse.StatusCode}");

        var runsPageHtml = await runsPageResponse.Content.ReadAsStringAsync(cancellationToken);
        var requestVerificationToken = ExtractRequestVerificationToken(runsPageHtml);
        Assert.False(string.IsNullOrWhiteSpace(requestVerificationToken),
            "Could not extract __RequestVerificationToken from /Runs page.");

        http.DefaultRequestHeaders.Remove("RequestVerificationToken");
        http.DefaultRequestHeaders.Add("RequestVerificationToken", requestVerificationToken);

        Output.WriteLine($"Starting API Health run-all via Automation.UI at {TestConfig.AutomationUiBase}");

        using var startResponse = await http.PostAsJsonAsync("api/api-health-runs/start-all",
            new { source = "BackendE2ETests.ApiStabilityTest" },
            cancellationToken);

        Assert.True(startResponse.IsSuccessStatusCode,
            $"POST /api/api-health-runs/start-all returned {(int)startResponse.StatusCode}: {await startResponse.Content.ReadAsStringAsync(cancellationToken)}");

        var startBody = await startResponse.Content.ReadFromJsonAsync<StartRunResponse>(JsonOpts, cancellationToken);
        Assert.NotNull(startBody);
        Assert.NotEqual(Guid.Empty, startBody.RunId);

        Output.WriteLine($"API Health run started: runId={startBody.RunId}");

        RunStatusResponse? statusBody = null;
        var deadline = DateTime.UtcNow.Add(Timeout);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, cancellationToken);

            using var statusResponse = await http.GetAsync($"api/api-health-runs/{startBody.RunId}/status", cancellationToken);
            Assert.True(statusResponse.IsSuccessStatusCode,
                $"GET /api/api-health-runs/{startBody.RunId}/status returned {(int)statusResponse.StatusCode}");

            statusBody = await statusResponse.Content.ReadFromJsonAsync<RunStatusResponse>(JsonOpts, cancellationToken);
            Assert.NotNull(statusBody);

            Output.WriteLine($"  status={statusBody.Status} total={statusBody.TotalEndpoints} passed={statusBody.PassedEndpoints} failed={statusBody.FailedEndpoints} skipped={statusBody.SkippedEndpoints}");

            if (statusBody.IsTerminal)
                break;
        }

        Assert.NotNull(statusBody);
        Assert.True(statusBody.IsTerminal,
            $"API Health run {startBody.RunId} did not complete within {Timeout.TotalMinutes} minutes.");

        if (statusBody.FailedResults.Count > 0)
        {
            foreach (var failure in statusBody.FailedResults.Take(20))
            {
                Output.WriteLine($"[API_HEALTH][FAIL] {failure.EndpointKey} | expected={failure.ExpectedStatusCode} actual={failure.ActualStatusCode?.ToString() ?? "n/a"} | {failure.ErrorMessage}");
            }
        }

        Assert.True(statusBody.Status == "Succeeded",
            $"API health run failed. status={statusBody.Status}, failedEndpoints={statusBody.FailedEndpoints}, error={statusBody.Error}");
    }

    private static int GetTimeoutMinutes()
    {
        var configured = Environment.GetEnvironmentVariable("API_STABILITY_TIMEOUT_MINUTES");
        if (!int.TryParse(configured, out var timeoutMinutes))
            return 30;

        return timeoutMinutes is >= 1 and <= 240 ? timeoutMinutes : 30;
    }

    private static string? ExtractRequestVerificationToken(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var match = Regex.Match(
            html,
            "name=['\"]__RequestVerificationToken['\"][^>]*value=['\"](?<token>[^'\"]+)['\"]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["token"].Value) : null;
    }

    private sealed class StartRunResponse
    {
        public Guid RunId { get; set; }
        public string Scope { get; set; } = string.Empty;
    }

    private sealed class RunStatusResponse
    {
        public Guid RunId { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string? ServiceName { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
        public int TotalEndpoints { get; set; }
        public int PassedEndpoints { get; set; }
        public int FailedEndpoints { get; set; }
        public int SkippedEndpoints { get; set; }
        public IReadOnlyList<FailedEndpointResult> FailedResults { get; set; } = [];
    }

    private sealed class FailedEndpointResult
    {
        public string EndpointKey { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string EndpointName { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int? ActualStatusCode { get; set; }
        public int ExpectedStatusCode { get; set; }
    }
}
