using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Three existing report scenarios in one Link.UI batch. Cleanup runs before a
/// run reports Succeeded, so report-entry patients and a FHIR read are taken
/// while the run is still going.
/// </summary>
public sealed class ConcurrentReportBatchTest : IClassFixture<BackendE2ETestFixture>
{
    private const int BatchCap = 3;
    private const string EhrSystem = "local";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);
    private static readonly Regex PatientIdPattern = new(
        "^Patient-(?<tag>[0-9a-fA-F]{8})-\\d{3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _sp;

    public ConcurrentReportBatchTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    [Fact]
    [Trait("Category", "ConcurrentReportBatch")]
    public async Task ExecuteConcurrentReportBatch()
    {
        using var timeoutCts = new CancellationTokenSource(Timeout);
        var cancellationToken = timeoutCts.Token;
        var output = _sp.GetRequiredService<ConsoleAutomationOutput>();
        using var link = new HttpClient
        {
            BaseAddress = new Uri(TestConfig.AutomationUiBase.TrimEnd('/') + "/")
        };

        var originalCap = await GetCapAsync(link, cancellationToken);
        var capChanged = false;
        try
        {
            if (originalCap != BatchCap)
            {
                await PutCapAsync(link, BatchCap, cancellationToken);
                capChanged = true;
            }

            output.WriteLine(
                $"Starting concurrent batch on {EhrSystem} at cap {BatchCap}: Adhoc, Daily ACH, Regenerate.");
            await RunBatchAsync(link, output, cancellationToken);
        }
        finally
        {
            if (capChanged)
                await PutCapAsync(link, originalCap, CancellationToken.None);
        }
    }

    private async Task RunBatchAsync(HttpClient link, ConsoleAutomationOutput output, CancellationToken cancellationToken)
    {
        using var startResponse = await link.PostAsJsonAsync(
            "api/report-batches",
            new
            {
                ehrSystem = EhrSystem,
                scenarioIds = new[]
                {
                    AutomationUiScenarioRunner.AdhocReportScenarioId,
                    AutomationUiScenarioRunner.AdhocReportDailyAchScenarioId,
                    AutomationUiScenarioRunner.RegenerateReportScenarioId
                }
            },
            cancellationToken);
        var startText = await startResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            startResponse.IsSuccessStatusCode,
            $"POST /api/report-batches returned {(int)startResponse.StatusCode}: {startText}");

        var batch = JsonSerializer.Deserialize<BatchBody>(startText, JsonOpts);
        Assert.NotNull(batch);
        Assert.Equal(BatchCap, batch.MaxConcurrency);
        Assert.Equal(3, batch.Items.Count);

        var watches = batch.Items.ToDictionary(
            item => item.ScenarioId,
            item => new RunWatch(item.ScenarioId, item.ScenarioName));
        var overlapped = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            using var batchResponse = await link.GetAsync($"api/report-batches/{batch.BatchId}", cancellationToken);
            var batchText = await batchResponse.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(
                batchResponse.IsSuccessStatusCode,
                $"GET /api/report-batches/{batch.BatchId} returned {(int)batchResponse.StatusCode}: {batchText}");
            batch = JsonSerializer.Deserialize<BatchBody>(batchText, JsonOpts);
            Assert.NotNull(batch);

            foreach (var item in batch.Items)
            {
                if (!watches.TryGetValue(item.ScenarioId, out var watch))
                    continue;
                watch.ItemStatus = item.Status;
                watch.ItemError = item.Error;
                if (item.RunId is Guid runId && runId != Guid.Empty)
                    watch.RunId = runId;
            }

            var started = new List<RunWatch>();
            foreach (var watch in watches.Values)
            {
                if (watch.RunId is not Guid runId)
                    continue;

                using var statusResponse = await link.GetAsync($"api/runs/{runId}/status", cancellationToken);
                var statusText = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
                Assert.True(
                    statusResponse.IsSuccessStatusCode,
                    $"GET /api/runs/{runId}/status returned {(int)statusResponse.StatusCode}: {statusText}");
                var status = JsonSerializer.Deserialize<RunStatusBody>(statusText, JsonOpts);
                Assert.NotNull(status);
                watch.Status = status.Status;
                watch.IsTerminal = status.IsTerminal;
                watch.StartedAt = status.StartedAt ?? watch.StartedAt;
                watch.FinishedAt = status.FinishedAt ?? watch.FinishedAt;
                watch.RunError = status.Error;
                if (!string.IsNullOrWhiteSpace(status.FacilityId))
                    watch.FacilityId = status.FacilityId;

                output.WriteLine(
                    $"  {watch.ScenarioName} status={status.Status} facility={watch.FacilityId ?? "(none)"}" +
                    (status.Error != null ? $" error={status.Error}" : ""));

                if (!status.IsTerminal && status.StartedAt != null)
                    started.Add(watch);

                if (!status.IsTerminal && !string.IsNullOrWhiteSpace(watch.FacilityId))
                    await SampleAsync(watch, output, cancellationToken);
            }

            if (started.Count == watches.Count)
                overlapped = true;

            if (batch.Items.Count > 0 && batch.Items.TrueForAll(item => IsTerminalItem(item.Status)))
                break;

            await Task.Delay(PollInterval, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Assert.NotNull(batch);
        Assert.True(overlapped, "The three runs were not all in progress at the same time.");
        Assert.Equal("Completed", batch.Status);
        Assert.True(
            batch.Items.TrueForAll(item => item.Status == "Succeeded"),
            "Batch items: " + string.Join("; ", batch.Items.Select(item => $"{item.ScenarioName}={item.Status} {item.Error}")));

        var runs = watches.Values.ToList();
        Assert.Equal(3, runs.Count);
        Assert.All(runs, run => Assert.False(string.IsNullOrWhiteSpace(run.FacilityId), $"{run.ScenarioName} has no facility id."));
        Assert.Equal(3, runs.Select(run => run.FacilityId).Distinct(StringComparer.Ordinal).Count());

        var latestStart = runs.Max(run => run.StartedAt);
        var earliestFinish = runs.Min(run => run.FinishedAt);
        Assert.True(
            latestStart != null && earliestFinish != null && latestStart < earliestFinish,
            "Run intervals do not overlap. " + string.Join("; ", runs.Select(run =>
                $"{run.ScenarioName} {run.StartedAt:o}..{run.FinishedAt:o}")));

        Assert.All(runs, run => Assert.True(
            run.Patients.Count > 0,
            $"{run.ScenarioName} finished before its facility had report entries."));

        foreach (var run in runs)
        {
            var tags = run.Patients.Select(TagOf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Assert.True(tags.Count == 1 && tags[0] != null, $"{run.ScenarioName} patient ids did not share one tag: {string.Join(", ", run.Patients)}");
            run.Tag = tags[0]!;
        }

        Assert.Equal(3, runs.Select(run => run.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var run in runs)
        {
            foreach (var other in runs)
            {
                if (ReferenceEquals(run, other))
                    continue;
                Assert.False(
                    run.Patients.Overlaps(other.Patients),
                    $"{run.ScenarioName} and {other.ScenarioName} share patients.");
            }
        }

        var facilities = _sp.GetRequiredService<IFacilityServiceClient>();
        using var fhir = new HttpClient();
        var fhirBase = TestConfig.FhirServerBase.TrimEnd('/');
        foreach (var run in runs)
        {
            Assert.True(run.FhirSeen, $"{run.ScenarioName} FHIR patients were not readable during the run.");
            var facility = await facilities.GetAsync(run.FacilityId!, cancellationToken);
            Assert.True(
                facility.StatusCode is 404 or 204 || (facility.IsSuccessStatusCode && facility.Body is { IsDeleted: true }),
                $"{run.ScenarioName} facility {run.FacilityId} is still present (HTTP {facility.StatusCode}).");

            foreach (var patientId in run.Patients)
            {
                using var patient = await fhir.GetAsync($"{fhirBase}/Patient/{Uri.EscapeDataString(patientId)}", cancellationToken);
                Assert.False(
                    patient.IsSuccessStatusCode,
                    $"{run.ScenarioName} patient {patientId} is still on the FHIR server (HTTP {(int)patient.StatusCode}).");
            }
        }
    }

    private async Task SampleAsync(RunWatch watch, ConsoleAutomationOutput output, CancellationToken cancellationToken)
    {
        var patients = await ReadReportEntriesAsync(watch.FacilityId!, cancellationToken);
        if (patients.Count == 0)
            return;

        var firstSample = watch.Patients.Count == 0;
        watch.Patients = patients;
        if (firstSample)
        {
            output.WriteLine(
                $"  {watch.ScenarioName} sampled facility {watch.FacilityId}: {patients.Count} report entry patient(s).");
        }

        if (watch.FhirSeen)
            return;

        using var fhir = new HttpClient();
        var fhirBase = TestConfig.FhirServerBase.TrimEnd('/');
        foreach (var patientId in patients)
        {
            using var patient = await fhir.GetAsync($"{fhirBase}/Patient/{Uri.EscapeDataString(patientId)}", cancellationToken);
            if (!patient.IsSuccessStatusCode)
                return;
        }

        watch.FhirSeen = true;
    }

    private async Task<HashSet<string>> ReadReportEntriesAsync(string facilityId, CancellationToken cancellationToken)
    {
        var reports = _sp.GetRequiredService<IReportServiceClient>();
        var patients = new HashSet<string>(StringComparer.Ordinal);
        const int pageSize = 100;

        for (var page = 1; page <= 20; page++)
        {
            var entries = await reports.SearchEntriesAsync(
                facilityId,
                pageSize: pageSize,
                pageNumber: page,
                cancellationToken: cancellationToken);
            Assert.True(
                entries.IsSuccessStatusCode,
                $"Report entries for {facilityId} returned HTTP {entries.StatusCode}.");
            if (entries.Body?.Records == null || entries.Body.Records.Count == 0)
                break;

            foreach (var entry in entries.Body.Records)
            {
                Assert.Equal(facilityId, entry.FacilityId);
                var patientId = NormalizePatientId(entry.PatientId);
                if (patientId != null)
                    patients.Add(patientId);
            }

            var totalPages = entries.Body.Metadata?.TotalPages ?? page;
            if (page >= totalPages)
                break;
        }

        return patients;
    }

    private static async Task<int> GetCapAsync(HttpClient link, CancellationToken cancellationToken)
    {
        using var response = await link.GetAsync("api/report-batch-settings", cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"GET /api/report-batch-settings returned {(int)response.StatusCode}: {text}");
        var body = JsonSerializer.Deserialize<CapBody>(text, JsonOpts);
        Assert.NotNull(body);
        return body.MaxConcurrency;
    }

    private static async Task PutCapAsync(HttpClient link, int cap, CancellationToken cancellationToken)
    {
        using var response = await link.PutAsJsonAsync(
            "api/report-batch-settings",
            new { maxConcurrency = cap },
            cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"PUT /api/report-batch-settings returned {(int)response.StatusCode}: {text}");
    }

    private static bool IsTerminalItem(string? status)
        => status is "Succeeded" or "Failed" or "Cancelled";

    private static string? NormalizePatientId(string? patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            return null;
        var trimmed = patientId.Trim();
        const string prefix = "Patient/";
        if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            trimmed = trimmed[prefix.Length..];
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? TagOf(string patientId)
    {
        var match = PatientIdPattern.Match(patientId);
        return match.Success ? match.Groups["tag"].Value : null;
    }

    private sealed class RunWatch(Guid scenarioId, string scenarioName)
    {
        public Guid ScenarioId { get; } = scenarioId;
        public string ScenarioName { get; } = scenarioName;
        public Guid? RunId { get; set; }
        public string? FacilityId { get; set; }
        public string? Status { get; set; }
        public string? ItemStatus { get; set; }
        public string? ItemError { get; set; }
        public string? RunError { get; set; }
        public bool IsTerminal { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Tag { get; set; }
        public bool FhirSeen { get; set; }
        public HashSet<string> Patients { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class BatchBody
    {
        public Guid BatchId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int MaxConcurrency { get; set; }
        public List<BatchItemBody> Items { get; set; } = [];
    }

    private sealed class BatchItemBody
    {
        public Guid ScenarioId { get; set; }
        public string ScenarioName { get; set; } = string.Empty;
        public Guid? RunId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    private sealed class RunStatusBody
    {
        public string Status { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Error { get; set; }
        public string? FacilityId { get; set; }
    }

    private sealed class CapBody
    {
        public int MaxConcurrency { get; set; }
    }
}
