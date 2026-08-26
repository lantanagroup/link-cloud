using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

internal static class AutomationUiScenarioRunner
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    internal static async Task RunScenarioAsync(
        IServiceProvider serviceProvider,
        Guid scenarioId,
        string scenarioName,
        string source,
        CancellationToken cancellationToken = default)
    {
        var output = serviceProvider.GetRequiredService<ConsoleAutomationOutput>();

        using var http = new HttpClient
        {
            BaseAddress = new Uri(TestConfig.AutomationUiBase.TrimEnd('/') + "/")
        };

        output.WriteLine($"Starting Automation.UI run for '{scenarioName}' ({scenarioId}) at {TestConfig.AutomationUiBase}");

        using var startResponse = await http.PostAsJsonAsync("api/runs/start",
            new StartScenarioApiRequest
            {
                ScenarioId = scenarioId,
                Source = source
            },
            cancellationToken);

        Assert.True(startResponse.IsSuccessStatusCode,
            $"POST /api/runs/start returned {(int)startResponse.StatusCode}: {await startResponse.Content.ReadAsStringAsync(cancellationToken)}");

        var startBody = await startResponse.Content.ReadFromJsonAsync<StartRunResponse>(JsonOpts, cancellationToken);
        Assert.NotNull(startBody);
        Assert.NotEqual(Guid.Empty, startBody.RunId);

        output.WriteLine($"Run started: runId={startBody.RunId} scenario={startBody.ScenarioName}");

        RunStatusResponse? statusBody = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(PollInterval, cancellationToken);

            using var statusResponse = await http.GetAsync($"api/runs/{startBody.RunId}/status", cancellationToken);
            Assert.True(statusResponse.IsSuccessStatusCode,
                $"GET /api/runs/{startBody.RunId}/status returned {(int)statusResponse.StatusCode}");

            statusBody = await statusResponse.Content.ReadFromJsonAsync<RunStatusResponse>(JsonOpts, cancellationToken);
            Assert.NotNull(statusBody);

            output.WriteLine($"  status={statusBody.Status} isTerminal={statusBody.IsTerminal}" +
                             (statusBody.Duration != null ? $" duration={statusBody.Duration}" : ""));

            if (statusBody.IsTerminal)
                break;
        }

        Assert.NotNull(statusBody);
        Assert.True(statusBody.Status == "Succeeded",
            $"Scenario '{scenarioName}' run failed. status={statusBody.Status}, error={statusBody.Error}");
    }

    internal static Guid AdhocReportScenarioId => new("00000000-0000-0000-0000-000000000001");
    internal static Guid AdhocReportDailyAchScenarioId => new("00000000-0000-0000-0000-000000000009");
    internal static Guid MultiPatientScenarioId => new("00000000-0000-0000-0000-000000000002");
    internal static Guid MegaPatientScenarioId => new("00000000-0000-0000-0000-000000000003");
    internal static Guid ScheduledReportScenarioId => new("00000000-0000-0000-0000-000000000004");
    internal static Guid RegenerateReportScenarioId => new("00000000-0000-0000-0000-000000000005");
    internal static Guid MultiMeasureScenarioId => new("00000000-0000-0000-0000-000000000006");
    internal static Guid MegaMultiPatientScenarioId => new("00000000-0000-0000-0000-000000000007");
    internal static Guid AdhocReportDailyAchScenarioId => new("00000000-0000-0000-0000-000000000009");

    private sealed class StartScenarioApiRequest
    {
        public Guid ScenarioId { get; set; }
        public string? Source { get; set; }
    }

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
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
    }
}
