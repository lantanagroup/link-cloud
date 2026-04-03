using System.Text;
using System.Text.Json;
using System.Globalization;
using Confluent.Kafka;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.DependencyInjection;
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

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"ScheduledTest-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private DualOutputHelper Output => _sp.GetRequiredService<DualOutputHelper>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public ReportScheduledWorkflowTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
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

        var validationApi = _sp.GetRequiredService<ValidationApiHelper>();
        await validationApi.InitializeArtifactsAsync();
        await validationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        if (_config.RemoveFacilityConfig)
        {
            await FacilitySetupHelper.CleanupFacilityAsync(
                _sp.GetRequiredService<IFacilityServiceClient>(),
                _sp.GetRequiredService<INormalizationServiceClient>(),
                _sp.GetRequiredService<IDataAcquisitionServiceClient>(),
                _sp.GetRequiredService<IQueryDispatchServiceClient>(),
                Output, _facilityId);
        }

        if (AutomationCfg.CleanupTestData)
        {
            await FacilitySetupHelper.CleanupQueryDispatchConfigAsync(
                _sp.GetRequiredService<IQueryDispatchServiceClient>(),
                Output,
                _facilityId);

            FhirDataLoader.ExpungeEverything(Output);
        }
    }

    [Fact]
    [Trait("Category", "ReportScheduledWorkflowTest")]
    public async Task ExecuteReportScheduledWorkflowTest()
    {
        var measureLoader = new MeasureLoader(
            _sp.GetRequiredService<IMeasureEvalServiceClient>(),
            _sp.GetRequiredService<IValidationServiceClient>(),
            Output, _config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        await FacilitySetupHelper.EnsureFacilityAsync(
            _sp.GetRequiredService<IFacilityServiceClient>(), Output, _facilityId, measureId);
        await FacilitySetupHelper.EnsureNormalizationConfigAsync(
            _sp.GetRequiredService<INormalizationServiceClient>(), Output, _facilityId);
        await FacilitySetupHelper.EnsureQueryPlansAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), Output, _facilityId, measureId, "Epic");
        await FacilitySetupHelper.EnsureQueryConfigAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), AutomationCfg, Output, _facilityId);
        await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
            _sp.GetRequiredService<IQueryDispatchServiceClient>(),
            Output,
            _facilityId);

        var reportId = await ProduceReportScheduledEventAsync(_facilityId, measureId, TimeSpan.FromMinutes(3));
        await WaitForScheduleCreationAsync(reportId);
        await ProduceAdmitPatientEventAsync(_facilityId, _config.PatientIds[0]);
        await Task.Delay(TimeSpan.FromMinutes(1));
        await ProduceDischargePatientEventAsync(_facilityId, _config.PatientIds[0]);

        var lokiScraper = _sp.GetRequiredService<LokiScraper>();
        var dataReader = _sp.GetRequiredService<PipelineDataReader>();
        var reportApi = _sp.GetRequiredService<ReportApiHelper>();

        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            Output, lokiScraper, AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false,
            pipelineReader: dataReader);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, Output);

        await diagnostics.StartAsync(_facilityId, reportId);
        var submitted = await reportApi.CheckSubmissionStatusAsync(reportId, _config, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        if (!submitted)
        {
            DumpKafkaTopicDiagnostics(diagnostics.KafkaErrors, "ResourceNormalized-Error", maxLines: 8);
        }

        var pipelineSnapshot = _sp.GetRequiredService<PipelineSnapshot>();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, reportId);

        Assert.True(submitted,
            $"Expected scheduled workflow report {reportId} to be submitted but it was not.");

        var schedule = await _sp.GetRequiredService<IReportServiceClient>().GetScheduleAsync(reportId)
            ?? throw new InvalidOperationException($"Schedule {reportId} not found after submission.");
        var actualStartDate = schedule.ReportStartDate.ToUniversalTime().ToString("o");
        var actualEndDate = schedule.ReportEndDate.ToUniversalTime().ToString("o");

        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, reportId, _config);
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, reportId, _config, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected scheduled workflow report to include manifest.ndjson but it was not");

        Output.WriteLine("Scheduled workflow artifact download succeeded.");

        foreach (var patientId in _config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected scheduled workflow report to include patient-{patientId}.ndjson but it was not");
        }

        await _sp.GetRequiredService<ReportAbsManifestValidator>().ValidateAllAsync(
            internalAbsResources,
            _config.PatientIds,
            measureId,
            actualStartDate,
            actualEndDate,
            _facilityId,
            reportId,
            GeneratedFhirDataSnapshotWriter.GetSnapshotDirectory(nameof(ReportScheduledWorkflowTest)));

        await ValidationBaselineManager.ValidateOrCreateAsync(
            Output, dataReader,
            nameof(ReportScheduledWorkflowTest),
            _facilityId,
            reportId,
            measureId,
            _config.PatientIds,
            _generatedBundles,
            internalAbsResources);

        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(
            _facilityId, reportId, measureId, _config.PatientIds,
            expectedFrequency: Frequency.Monthly,
            expectedAdHocType: null);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(_facilityId, reportId, measureId, _config.PatientIds);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureId);
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(_facilityId, reportId, _config.PatientIds, _config.LokiScrapeWindow);
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
                    Output.WriteLine($"Producing ReportScheduled directly to Kafka (facility={facilityId}, reportId={reportId}, delay={delay.TotalMinutes:F1}m, bootstrap={bootstrapServers}, mode={config.mode})...");

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
        var delay = TimeSpan.FromSeconds(2);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            var schedule = await _sp.GetRequiredService<IReportServiceClient>().GetScheduleAsync(reportId);
            if (schedule != null)
            {
                Output.WriteLine($"Report schedule detected before patient-event publish (status={schedule.Status}).");
                return;
            }

            await Task.Delay(delay);
        }

        throw new TimeoutException($"Timed out waiting for report schedule {reportId} to be created.");
    }

    private Task ProduceAdmitPatientEventAsync(string facilityId, string patientId)
        => ProducePatientEventAsync(facilityId, patientId, "Admit");

    private async Task ProduceDischargePatientEventAsync(string facilityId, string patientId)
        => await ProducePatientEventAsync(facilityId, patientId, "Discharge");

    private async Task ProducePatientEventAsync(string facilityId, string patientId, string eventType)
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
                    Output.WriteLine($"Producing PatientEvent directly to Kafka (facility={facilityId}, patient={patientId}, event={eventType}, bootstrap={bootstrapServers}, mode={config.mode})...");

                    using var producer = new ProducerBuilder<string, string>(config.producerConfig).Build();

                    var payload = JsonSerializer.Serialize(new
                    {
                        PatientId = patientId,
                        EventType = eventType
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
