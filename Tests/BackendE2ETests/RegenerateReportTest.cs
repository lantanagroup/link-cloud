using Flurl.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Confluent.Kafka;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.SerDes;
using RestSharp;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end test for the regenerate workflow.
///
/// Source report follows the production scheduled-report path:
///   ReportScheduled → PatientEvent (Admit) → DataAcq → full pipeline → Submitted.
///
/// Regenerated report exercises:
///   GenerateReportListener (Regenerate=true) → EvaluationRequested → MeasureEval reuses prior data.
/// </summary>
public sealed class RegenerateReportTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260401;

    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "REGENERATE_REPORT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxRetryCount: 140,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly TestServices _b;
    private readonly string _facilityId = $"RegenTest-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];

    private AutomationConfig AutomationCfg => _b.AutomationCfg;
    private DualOutputHelper Output => _b.Output;
    private FhirDataLoader FhirDataLoader => _b.FhirDataLoader;

    private ValidationApiHelper ValidationApi => _b.CreateValidationHelper();

    public RegenerateReportTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
        _config.RemoveFacilityConfig = true;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var (patientIds, bundles) = FhirBundleGenerator.Generate(Output, 1, 100, "RegenPatient", GenerationSeed);
        _generatedBundles = bundles;

        if (_config.PatientIds.Count == 0)
            _config.PatientIds = patientIds;

        await GeneratedFhirDataSnapshotWriter.WriteIfChangedAsync(
            Output,
            nameof(RegenerateReportTest),
            GenerationSeed,
            _config.PatientIds,
            bundles);

        await FhirDataLoader.WaitForServerAsync(Output);
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(Output, bundles);

        await ValidationApi.InitializeArtifactsAsync();
        await ValidationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        if (_config.RemoveFacilityConfig)
            await SdkSetupHelper.CleanupFacilityAsync(_b, _facilityId);

        if (AutomationCfg.CleanupTestData)
            FhirDataLoader.ExpungeEverything(Output);
    }

    [Fact]
    [Trait("Category", "RegenerateReportTest")]
    public async Task ExecuteRegenerateReportTest()
    {
        var measureLoader = new MeasureLoader(_b.MeasureEvalClient, _b.SdkValidationClient, Output, _config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        // Step 1: Set up facility and all service configurations.
        await SdkSetupHelper.EnsureFacilityAsync(_b, _facilityId, measureId);
        await SdkSetupHelper.EnsureNormalizationConfigAsync(_b, _facilityId);
        await SdkSetupHelper.EnsureQueryPlansAsync(_b, _facilityId, measureId, "Epic");
        await SdkSetupHelper.EnsureQueryConfigAsync(_b, _facilityId);

        // Step 2: Create source report via the scheduled-report production path.
        //   a) Produce ReportScheduled → creates the report schedule.
        //   b) Wait for the schedule to exist so PatientEventListener can find it.
        //   c) Produce PatientEvent (Admit) → PatientEventListener adds entries → DataAcq → full pipeline.
        var sourceReportId = await ProduceReportScheduledEventAsync(_facilityId, measureId, TimeSpan.FromMinutes(1));
        await WaitForScheduleCreationAsync(sourceReportId);
        await ProduceAdmitPatientEventAsync(_facilityId, _config.PatientIds[0]);

        await using var sourceDiagnostics = new BackgroundDiagnosticsMonitor(
            Output,
            _b.LokiScraper,
            AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var sourceWatcher = DiagnosticsEventWatcher.Start(sourceDiagnostics, Output);

        await sourceDiagnostics.StartAsync(_facilityId, sourceReportId);
        var sourceSubmitted = await _b.CreateReportHelper(_config).CheckSubmissionStatusAsync(sourceReportId, sourceDiagnostics);
        await sourceDiagnostics.StopAsync();
        await sourceWatcher.StopAsync();

        Assert.True(sourceSubmitted, $"Source report {sourceReportId} was not submitted.");
        Output.WriteLine($"Source report {sourceReportId} submitted successfully.\n");

        // Step 3: Regenerate report from the completed source.
        // This exercises: GenerateReportListener (Regenerate=true) -> EvaluationRequested -> MeasureEval reuses prior data.
        var regeneratedReportId = await RegenerateAsync(_facilityId, sourceReportId);

        await using var regenDiagnostics = new BackgroundDiagnosticsMonitor(
            Output,
            _b.LokiScraper,
            AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var regenWatcher = DiagnosticsEventWatcher.Start(regenDiagnostics, Output);

        await regenDiagnostics.StartAsync(_facilityId, regeneratedReportId);
        var regeneratedSubmitted = await _b.CreateReportHelper(_config).CheckSubmissionStatusAsync(regeneratedReportId, regenDiagnostics);
        await regenDiagnostics.StopAsync();
        await regenWatcher.StopAsync();

        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, regeneratedReportId);

        Assert.True(regeneratedSubmitted,
            $"Expected regenerated report {regeneratedReportId} to be submitted but it was not.");

        // Step 4: Validate regenerated report artifacts.
        var reportApi = _b.CreateReportHelper(_config);
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, regeneratedReportId);
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, regeneratedReportId, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected regenerated report to include manifest.ndjson but it was not");

        foreach (var patientId in _config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected regenerated report to include patient-{patientId}.ndjson but it was not");
        }

        await ValidationBaselineManager.ValidateOrCreateAsync(
            Output,
            _b.DataReader,
            nameof(RegenerateReportTest),
            _facilityId,
            regeneratedReportId,
            measureId,
            _config.StartDate,
            _config.EndDate,
            _config.PatientIds,
            _generatedBundles,
            internalAbsResources);

        // Step 5: Database-level validation.
        await _b.CreateReportValidator().ValidateAllAsync(
            _facilityId,
            regeneratedReportId,
            measureId,
            _config.PatientIds,
            expectedFrequency: Frequency.Adhoc);
        await _b.CreateDataAcqValidator().ValidateAllAsync(_facilityId, regeneratedReportId, measureId, _config.PatientIds, expectDataAcquisitionData: false);
        await _b.CreateNormalizationValidator().ValidateAllAsync(_facilityId);
        await _b.CreateTenantValidator().ValidateAllAsync(_facilityId, measureId);
        await _b.CreateValidationResultsValidator().ValidateAllAsync(_facilityId, regeneratedReportId, _config.PatientIds, _config.LokiScrapeWindow);
    }

    private async Task<string> ProduceReportScheduledEventAsync(string facilityId, string measureId, TimeSpan delay)
    {
        var reportId = Guid.NewGuid();
        var startDateUtc = DateTime.SpecifyKind(DateTime.Parse(_config.StartDate, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var endDateUtc = DateTime.UtcNow.Add(delay);

        var candidates = new[]
        {
            AutomationCfg.Kafka.BootstrapServers,
            "localhost:9092",
            "localhost:9094"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        Exception? last = null;

        foreach (var bootstrapServers in candidates)
        {
            foreach (var config in BuildProducerConfigs(bootstrapServers))
            {
                try
                {
                    Output.WriteLine($"Producing ReportScheduled directly to Kafka (facility={facilityId}, reportId={reportId}, delay={delay.TotalMinutes:F1}m, bootstrap={bootstrapServers}, mode={config.mode})…");

                    using var producer = new ProducerBuilder<string, ReportScheduledValue>(config.producerConfig)
                        .SetValueSerializer(new JsonWithFhirMessageSerializer<ReportScheduledValue>())
                        .Build();

                    await producer.ProduceAsync(nameof(KafkaTopic.ReportScheduled), new Message<string, ReportScheduledValue>
                    {
                        Key = facilityId,
                        Value = new ReportScheduledValue
                        {
                            ReportTypes = [measureId],
                            Frequency = Frequency.Monthly,
                            StartDate = new DateTimeOffset(startDateUtc),
                            EndDate = new DateTimeOffset(endDateUtc),
                            ReportTrackingId = reportId
                        },
                        Headers = new Headers
                        {
                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(reportId.ToString()) }
                        }
                    });

                    producer.Flush(TimeSpan.FromSeconds(5));
                    return reportId.ToString();
                }
                catch (Exception ex)
                {
                    last = ex;
                    Output.WriteLine($"Kafka produce attempt failed for bootstrap '{bootstrapServers}' ({config.mode}): {ex.Message}");
                }
            }
        }

        throw new InvalidOperationException(
            $"Unable to produce ReportScheduled to Kafka using any bootstrap server candidate: {string.Join(", ", candidates)}",
            last);
    }

    private async Task WaitForScheduleCreationAsync(string reportId)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var poll = TimeSpan.FromSeconds(2);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            try
            {
                var schedule = await _b.ReportClient.GetScheduleAsync(reportId);
                var scheduleStatus = schedule.Status.ToString();

                if (string.Equals(scheduleStatus, "EndOfPeriod", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Report schedule {reportId} reached EndOfPeriod before PatientEvent publish. " +
                        "Increase ReportScheduled delay to keep the schedule open long enough for event ingestion.");
                }

                Output.WriteLine($"Report schedule detected (status={scheduleStatus}, after {(DateTime.UtcNow - started).TotalSeconds:F1}s).");
                return;
            }
            catch (FlurlHttpException ex) when (ex.StatusCode == 404)
            {
                // not visible yet — keep polling
            }

            await Task.Delay(poll);
        }

        throw new TimeoutException($"Timed out waiting for report schedule {reportId} to be created.");
    }

    private async Task ProduceAdmitPatientEventAsync(string facilityId, string patientId)
    {
        var candidates = new[]
        {
            AutomationCfg.Kafka.BootstrapServers,
            "localhost:9092",
            "localhost:9094"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        Exception? last = null;

        foreach (var bootstrapServers in candidates)
        {
            foreach (var config in BuildProducerConfigs(bootstrapServers))
            {
                try
                {
                    Output.WriteLine($"Producing PatientEvent (facility={facilityId}, patient={patientId}, event=Admit, bootstrap={bootstrapServers}, mode={config.mode})…");

                    using var producer = new ProducerBuilder<string, string>(config.producerConfig).Build();

                    var payload = JsonSerializer.Serialize(new
                    {
                        PatientId = patientId,
                        EventType = "Admit"
                    });

                    await producer.ProduceAsync(nameof(KafkaTopic.PatientEvent), new Message<string, string>
                    {
                        Key = facilityId,
                        Value = payload,
                        Headers = new Headers
                        {
                            { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                        }
                    });

                    producer.Flush(TimeSpan.FromSeconds(5));
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                    Output.WriteLine($"Kafka produce attempt failed for bootstrap '{bootstrapServers}' ({config.mode}): {ex.Message}");
                }
            }
        }

        throw new InvalidOperationException(
            $"Unable to produce PatientEvent to Kafka using any bootstrap server candidate: {string.Join(", ", candidates)}",
            last);
    }

    private async Task<string> RegenerateAsync(string facilityId, string sourceReportId)
    {
        Output.WriteLine($"Regenerating report from source reportId={sourceReportId}…");

        var request = new RestRequest($"facility/{facilityId}/RegenerateReport", Method.Post)
            .AddJsonBody(new RegenerateReportRequest
            {
                ReportId = sourceReportId,
                BypassSubmission = false
            });

        var response = await _b.AdminBffClient.ExecuteAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Content));

        var payload = JsonSerializer.Deserialize<GenerateAdhocReportResponseApiModel>(
            response.Content!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.ReportId);

        return payload.ReportId.ToString();
    }

    private IEnumerable<(ProducerConfig producerConfig, String mode)> BuildProducerConfigs(string bootstrapServers)
    {
        var hasCredentials =
            !string.IsNullOrWhiteSpace(AutomationCfg.Kafka.User) &&
            !string.IsNullOrWhiteSpace(AutomationCfg.Kafka.Password);

        var allowPlaintextFallback = bool.TryParse(
            Environment.GetEnvironmentVariable("E2E_KAFKA_ALLOW_PLAINTEXT_FALLBACK"),
            out var parsedAllowPlaintextFallback) && parsedAllowPlaintextFallback;

        if (hasCredentials)
        {
            yield return (new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                MessageTimeoutMs = 10000,
                SecurityProtocol = SecurityProtocol.SaslPlaintext,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = AutomationCfg.Kafka.User,
                SaslPassword = AutomationCfg.Kafka.Password
            }, "sasl_plaintext");

            if (!allowPlaintextFallback)
                yield break;
        }

        yield return (new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            MessageTimeoutMs = 10000
        }, "plaintext");
    }
}
