using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Shared.Application.Models;
using RestSharp;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end test for ReportScheduledListener path:
/// produce ReportScheduled integration event, create patient entry via PatientEvent,
/// and validate full pipeline completion after Quartz executes EndOfReportPeriodJob.
/// </summary>
public sealed class ReportScheduledWorkflowTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260326;

    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "SCHEDULED_REPORT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxRetryCount: 140,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly TestServices _b;
    private readonly string _facilityId = $"ScheduledTest-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];

    private AutomationConfig AutomationCfg => _b.AutomationCfg;
    private DualOutputHelper Output => _b.Output;
    private FhirDataLoader FhirDataLoader => _b.FhirDataLoader;

    private FacilityApiClient FacilityApi => _b.CreateFacilityApi();
    private NormalizationApiClient NormalizationApi => _b.CreateNormalizationApi();
    private QueryConfigApiClient QueryConfigApi => _b.CreateQueryConfigApi();
    private ValidationApiClient ValidationApi => _b.CreateValidationApi();

    public ReportScheduledWorkflowTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
        _config.RemoveFacilityConfig = true;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var (patientIds, bundles) = FhirBundleGenerator.Generate(Output, 1, 1000, "ScheduledPatient", GenerationSeed);
        _generatedBundles = bundles;

        if (_config.PatientIds.Count == 0)
            _config.PatientIds = patientIds;

        await GeneratedFhirDataSnapshotWriter.WriteIfChangedAsync(
            Output,
            nameof(ReportScheduledWorkflowTest),
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
            await FacilityApi.DeleteAsync(_facilityId);

        if (AutomationCfg.CleanupTestData)
            FhirDataLoader.ExpungeEverything(Output);
    }

    [Fact]
    [Trait("Category", "ReportScheduledWorkflowTest")]
    public async Task ExecuteReportScheduledWorkflowTest()
    {
        var measureLoader = new MeasureLoader(_b.MeasureEvalClient, _b.SdkValidationClient, Output, _config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        await FacilityApi.CreateAsync(_facilityId, measureId);
        await NormalizationApi.CreateConfigAsync(_facilityId);
        await QueryConfigApi.CreateQueryPlanAsync(_facilityId, measureId, "Epic");
        await QueryConfigApi.CreateQueryConfigAsync(_facilityId);

        var reportId = await ProduceReportScheduledEventAsync(_facilityId, measureId, TimeSpan.FromMinutes(2));
        await WaitForScheduleCreationAsync(reportId);
        await ProduceAdmitPatientEventAsync(_facilityId, _config.PatientIds[0]);

        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            Output,
            _b.LokiScraper,
            AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, Output);

        await diagnostics.StartAsync(_facilityId, reportId);
        var submitted = await _b.CreateReportApi(_config).CheckSubmissionStatusAsync(reportId, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        if (!submitted)
        {
            DumpKafkaTopicDiagnostics(diagnostics.KafkaErrors, "ResourceNormalized-Error", maxLines: 8);
        }

        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, reportId);

        Assert.True(submitted,
            $"Expected scheduled workflow report {reportId} to be submitted but it was not.");

        var reportApi = _b.CreateReportApi(_config);
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, reportId);
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, reportId, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected scheduled workflow report to include manifest.ndjson but it was not");

        Output.WriteLine("Scheduled workflow artifact download succeeded.");

        foreach (var patientId in _config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected scheduled workflow report to include patient-{patientId}.ndjson but it was not");
        }

        await ValidationBaselineManager.ValidateOrCreateAsync(
            Output,
            _b.DataReader,
            nameof(ReportScheduledWorkflowTest),
            _facilityId,
            reportId,
            measureId,
            _config.StartDate,
            _config.EndDate,
            _config.PatientIds,
            _generatedBundles,
            internalAbsResources);

        await _b.CreateReportValidator().ValidateAllAsync(
            _facilityId,
            reportId,
            measureId,
            _config.PatientIds,
            expectedFrequency: Frequency.Monthly,
            expectedAdHocType: null);
        await _b.CreateDataAcqValidator().ValidateAllAsync(_facilityId, reportId, measureId, _config.PatientIds);
        await _b.CreateNormalizationValidator().ValidateAllAsync(_facilityId);
        await _b.CreateTenantValidator().ValidateAllAsync(_facilityId, measureId);
        await _b.CreateValidationResultsValidator().ValidateAllAsync(_facilityId, reportId, _config.PatientIds, _config.LokiScrapeWindow);
    }

    private async Task<string> ProduceReportScheduledEventAsync(string facilityId, string measureId, TimeSpan delay)
    {
        var reportId = Guid.NewGuid().ToString();
        var startDate = DateTime.SpecifyKind(DateTime.Parse(_config.StartDate, CultureInfo.InvariantCulture), DateTimeKind.Utc);

        Output.WriteLine($"Producing integration report-scheduled event (facility={facilityId}, reportId={reportId}, delay={delay.TotalMinutes:F1}m)...");

        var request = new RestRequest("integration/report-scheduled", Method.Post)
            .AddJsonBody(new
            {
                facilityId,
                frequency = Frequency.Monthly,
                reportTypes = new[] { measureId },
                startDate,
                delay = delay.TotalMinutes.ToString(CultureInfo.InvariantCulture),
                reportTrackingId = reportId
            });

        var response = await _b.AdminBffClient.ExecuteAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return reportId;
    }

    private async Task WaitForScheduleCreationAsync(string reportId)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var delay = TimeSpan.FromSeconds(2);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var (status, schedule) = await _b.ReportClient.GetScheduleAsync(reportId);
            if (status == HttpStatusCode.OK && schedule != null)
            {
                Output.WriteLine($"Report schedule detected before patient-event publish (status={schedule.Status}).");
                return;
            }

            await Task.Delay(delay);
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
                    Output.WriteLine($"Producing PatientEvent directly to Kafka (facility={facilityId}, patient={patientId}, event=Admit, bootstrap={bootstrapServers}, mode={config.mode})...");

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

    private IEnumerable<(ProducerConfig producerConfig, String mode)> BuildProducerConfigs(String bootstrapServers)
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

    private void DumpKafkaTopicDiagnostics(IReadOnlyList<string> kafkaErrors, string topic, int maxLines)
    {
        var filtered = kafkaErrors
            .Where(x => x.Contains($"[Kafka][{topic}]", StringComparison.OrdinalIgnoreCase))
            .Take(maxLines)
            .ToList();

        Output.WriteLine($"[DIAG][FocusedKafka] {topic}: captured={filtered.Count}");
        foreach (var line in filtered)
        {
            Output.WriteLine(line);
        }
    }
}
