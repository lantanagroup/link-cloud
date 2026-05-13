using System.Text;
using System.Text.Json;
using System.Globalization;
using Confluent.Kafka;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end test for the regenerate workflow.
/// </summary>
public sealed class RegenerateReportTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260401;

    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "REGENERATE_REPORT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxPollingDurationMinutes: 7,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"RegenTest-{Guid.NewGuid():N}";
    private List<ProfiledMeasureType> _measures = [];
    private GenerationManifest? _generationManifest;

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private ConsoleAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public RegenerateReportTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
        _config.CleanupServiceData = true;
    }

    public async Task InitializeAsync()
    {
        _sp.GetRequiredService<PipelineDataReader>().InvalidateCache();

        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var measures = new List<ProfiledMeasureType> { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        _measures = measures;
        var cohorts = new List<PatientCohortDefinition>
        {
            PatientCohortDefinition.AllQualifying(measures, patientCount: 1, resourcesMin: 100, resourcesMax: 100)
        };
        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, GenerationSeed);

        await FhirDataLoader.WaitForServerAsync(Output);

        var pipelineResult = await FhirGenerationPipeline.GenerateAndUploadAsync(
            Output,
            FhirDataLoader,
            measures,
            profiles,
            totalResourcesPerPatient: profiles[0].ResourcesPerPatient ?? 100,
            generationSeed: GenerationSeed,
            acquisitionSimulation: new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = QueryPlanBuilder.GetDefaultAsInput(),
                ClinicalPeriodStart = _config.StartDate,
                ClinicalPeriodEnd = _config.EndDate
            });

        _generationManifest = pipelineResult.Manifest;

        if (_config.PatientIds.Count == 0)
            _config.PatientIds = pipelineResult.PatientIds;

        var validationApi = _sp.GetRequiredService<ValidationApiHelper>();
        await validationApi.InitializeArtifactsAsync();
        await validationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        await RunCleanupHelper.CleanupAfterRunAsync(
            _config,
            _sp.GetRequiredService<IFacilityServiceClient>(),
            _sp.GetRequiredService<INormalizationServiceClient>(),
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(),
            _sp.GetRequiredService<IQueryDispatchServiceClient>(),
            _sp.GetRequiredService<IReportServiceClient>(),
            FhirDataLoader,
            Output,
            _facilityId,
            null);
    }

    [Fact]
    [Trait("Category", "RegenerateReportTest")]
    public async Task ExecuteRegenerateReportTest()
    {
        var measureLoader = new MeasureLoader(
            _sp.GetRequiredService<IMeasureEvalServiceClient>(),
            _sp.GetRequiredService<IValidationServiceClient>(),
            Output, _config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        // Step 1: Set up facility and all service configurations.
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

        // Step 2: Create source report via the scheduled-report production path.
        //   a) Produce ReportScheduled ? creates the report schedule.
        //   b) Wait for the schedule to exist so PatientEventListener can find it.
        //   c) Produce PatientEvent (Admit) ? PatientEventListener adds entries ? DataAcq ? full pipeline.
        var sourceReportId = await ProduceReportScheduledEventAsync(_facilityId, measureId, TimeSpan.FromMinutes(1));
        await WaitForScheduleCreationAsync(sourceReportId);
        await ProduceAdmitPatientEventAsync(_facilityId, _config.PatientIds[0]);

        var lokiScraper = _sp.GetRequiredService<LokiScraper>();
        var dataReader = _sp.GetRequiredService<PipelineDataReader>();
        var reportApi = _sp.GetRequiredService<ReportApiHelper>();

        await using var sourceDiagnostics = new BackgroundDiagnosticsMonitor(
            Output, lokiScraper, AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false,
            pipelineReader: dataReader);
        await using var sourceWatcher = DiagnosticsEventWatcher.Start(sourceDiagnostics, Output);

        await sourceDiagnostics.StartAsync(_facilityId, sourceReportId);
        var sourceSubmitted = await reportApi.CheckSubmissionStatusAsync(sourceReportId, _config, sourceDiagnostics);
        await sourceDiagnostics.StopAsync();
        await sourceWatcher.StopAsync();

        Assert.True(sourceSubmitted, $"Source report {sourceReportId} was not submitted.");
        Output.WriteLine($"Source report {sourceReportId} submitted successfully.\n");

        // Step 3: Regenerate report from the completed source.
        // This exercises: GenerateReportListener (Regenerate=true) -> EvaluationRequested -> MeasureEval reuses prior data.
        var regeneratedReportId = await RegenerateAsync(_facilityId, sourceReportId);

        await using var regenDiagnostics = new BackgroundDiagnosticsMonitor(
            Output, lokiScraper, AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false,
            pipelineReader: dataReader,
            expectsDataAcquisition: false);
        await using var regenWatcher = DiagnosticsEventWatcher.Start(regenDiagnostics, Output);

        await regenDiagnostics.StartAsync(_facilityId, regeneratedReportId);
        var regeneratedSubmitted = await reportApi.CheckSubmissionStatusAsync(regeneratedReportId, _config, regenDiagnostics);
        await regenDiagnostics.StopAsync();
        await regenWatcher.StopAsync();

        var pipelineSnapshot = _sp.GetRequiredService<PipelineSnapshot>();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, regeneratedReportId);

        Assert.True(regeneratedSubmitted,
            $"Expected regenerated report {regeneratedReportId} to be submitted but it was not.");

        var regenSchedule = await _sp.GetRequiredService<IReportServiceClient>().GetScheduleAsync(regeneratedReportId)
            ?? throw new InvalidOperationException($"Schedule {regeneratedReportId} not found after submission.");
        var actualStartDate = regenSchedule.ReportStartDate.ToUniversalTime().ToString("o");
        var actualEndDate = regenSchedule.ReportEndDate.ToUniversalTime().ToString("o");

        // Step 4: Validate regenerated report artifacts.
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, regeneratedReportId, _config);
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, regeneratedReportId, _config, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected regenerated report to include manifest.ndjson but it was not");

        foreach (var patientId in _config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected regenerated report to include patient-{patientId}.ndjson but it was not");
        }

        // Flush stale cache from diagnostics polling so validators read authoritative data.
        dataReader.InvalidateCache();

        var generationManifest = _generationManifest
            ?? throw new InvalidOperationException("Generation manifest was not produced by the pipeline.");
        generationManifest.MeasureIds = [measureId];
        var queryPlanInput = QueryPlanBuilder.GetDefaultAsInput();
        generationManifest.AcquiredResourceTypes = QueryPlanBuilder.GetAcquiredResourceTypes(queryPlanInput);
        generationManifest.ParameterQueryResourceTypes = QueryPlanBuilder.GetParameterQueryResourceTypes(queryPlanInput);
        generationManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(_measures);

        await _sp.GetRequiredService<ReportAbsManifestValidator>().ValidateAllAsync(
            internalAbsResources,
            _config.PatientIds,
            measureId,
            actualStartDate,
            actualEndDate,
            _facilityId,
            regeneratedReportId,
            generatedBundles: null,
            expectDataAcquisitionData: false,
            manifest: generationManifest);

        // Step 5: Database-level validation.
        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(
            _facilityId, regeneratedReportId,
            measureId,
            _config.PatientIds,
            expectedFrequency: Frequency.Adhoc);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(_facilityId, regeneratedReportId, measureId, _config.PatientIds, expectDataAcquisitionData: false);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureId);
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(_facilityId, regeneratedReportId, _config.PatientIds, _config.LokiScrapeWindow);
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
            var schedule = await _sp.GetRequiredService<IReportServiceClient>().GetScheduleAsync(reportId);
            if (schedule != null)
            {
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

        var payload = await _sp.GetRequiredService<IFacilityServiceClient>().RegenerateReportAsync(facilityId, new RegenerateReportRequest
        {
            ReportId = sourceReportId,
            BypassSubmission = false
        });

        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.ReportId);

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
