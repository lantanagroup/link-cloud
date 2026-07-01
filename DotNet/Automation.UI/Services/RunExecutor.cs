using Automation.UI.Models;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

/// <summary>
/// Executes the long-running automation pipeline for a single in-flight run:
/// per-run DI scope, FHIR generation/upload, facility setup, report generation
/// (and optional regeneration), validators, and cleanup.
/// <para>
/// Extracted from <see cref="AutomationRunManager"/> so the lifecycle/state
/// concerns stay in the manager and the pipeline orchestration lives here in
/// one focused class. The executor never touches the in-process run dictionary
/// or SignalR directly &mdash; it talks to the manager through the
/// <see cref="ExecutorCallbacks"/> hooks below, which lets the manager keep
/// authoritative ownership of state mutation, broadcast, and persistence.
/// </para>
/// </summary>
internal sealed class RunExecutor
{
    private readonly AutomationConfig _automationConfig;
    private readonly IServiceProvider _hostServices;
    private readonly ISnapshotStore _snapshotStore;
    private readonly RunSnapshotOrchestrator _orchestrator;
    private readonly QueryPlanTemplateResolver _queryPlanResolver;
    private readonly NormalizationSuiteResolver _normalizationSuiteResolver;
    private readonly bool _suppressExternalManifest;
    private readonly ILogger _logger;

    public RunExecutor(
        AutomationConfig automationConfig,
        IServiceProvider hostServices,
        ISnapshotStore snapshotStore,
        RunSnapshotOrchestrator orchestrator,
        QueryPlanTemplateResolver queryPlanResolver,
        NormalizationSuiteResolver normalizationSuiteResolver,
        IConfiguration configuration,
        ILogger logger)
    {
        _automationConfig = automationConfig;
        _hostServices = hostServices;
        _snapshotStore = snapshotStore;
        _orchestrator = orchestrator;
        _queryPlanResolver = queryPlanResolver;
        _normalizationSuiteResolver = normalizationSuiteResolver;
        _suppressExternalManifest = configuration.GetValue<bool>("ExternalBlobStorage:SuppressManifest");
        _logger = logger;
    }

    /// <summary>
    /// Hooks the executor uses to talk back to the owning <see cref="AutomationRunManager"/>.
    /// Keeping these as delegates (rather than a manager reference) avoids a circular
    /// dependency and makes the executor's collaboration surface explicit.
    /// </summary>
    public sealed record ExecutorCallbacks(
        IAutomationOutput Output,
        Func<Task> BroadcastStatus,
        Func<Task> PersistRunSummary);

    public async Task ExecuteAsync(
        MutableRunState state,
        ExecutorCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        var output = callbacks.Output;

        state.Status = AutomationRunStatus.Running;
        state.StartedAt = DateTimeOffset.UtcNow;
        await callbacks.BroadcastStatus();

        try
        {
            var scenarioConfig = ScenarioConfigBuilder.Build(state.Scenario, state.Options);

            using var services = BuildRunServiceProvider(output);

            var lokiScraper = services.GetRequiredService<LokiScraper>();
            var fhirDataLoader = services.GetRequiredService<FhirDataLoader>();
            state.FhirDataLoader = fhirDataLoader;
            var measureEvalClient = services.GetRequiredService<IMeasureEvalServiceClient>();
            var sdkValidationClient = services.GetRequiredService<IValidationServiceClient>();

            var reportHelper = services.GetRequiredService<ReportApiHelper>();

            var validationHelper = services.GetRequiredService<ValidationApiHelper>();
            var reportValidator = services.GetRequiredService<ReportDatabaseValidator>();
            var reportAbsValidator = services.GetRequiredService<ReportAbsManifestValidator>();
            var dataAcqValidator = services.GetRequiredService<DataAcquisitionDatabaseValidator>();
            var normalizationValidator = services.GetRequiredService<NormalizationDatabaseValidator>();
            var tenantValidator = services.GetRequiredService<TenantDatabaseValidator>();
            var validationResultsValidator = services.GetRequiredService<ValidationResultsValidator>();
            var pipelineSnapshot = services.GetRequiredService<PipelineSnapshot>();

            output.WriteLine($"Starting {state.Scenario} run: {state.RunId}");
            output.WriteLine($"Measure context: {string.Join(", ", state.Options.SelectedMeasures.Select(m => $"{ProfiledMeasureCatalog.GetDisplayName(m)} ({m})"))}");
            output.WriteLine($"Generation config: patients={state.Options.PatientCount}, resourcesPerPatient={state.Options.ResourcesPerPatient}, seed={state.Options.Seed}");

            List<string> patientIds;
            List<string> expectedSubmittedPatientIds;
            GenerationManifest? generationManifest = null;

            // Use the first measure for generation context (profile-driven generation picks
            // the most restrictive measure — patients qualifying for all measures must meet
            // the criteria of each). For multi-measure, the pipeline handles the union.
            var primaryMeasure = state.Options.SelectedMeasures[0];
            var generationConfig = ResolveFhirGenerationConfig(_automationConfig);

            await fhirDataLoader.WaitForServerAsync(output);

            // Resolve the query plan template early so the acquisition simulator uses the
            // same plan the scenario is configured with (not always the built-in default).
            var queryPlanResolution = await _queryPlanResolver.ResolveAsync(state.Options.QueryPlanTemplateId, cancellationToken);
            var queryPlanInput = queryPlanResolution.Input;
            var effectiveQueryPlan = queryPlanInput ?? QueryPlanDefaults.GetDefaultAsInput();
            if (!string.IsNullOrWhiteSpace(queryPlanResolution.Name))
                output.WriteLine($"Using query plan: {queryPlanResolution.Name}");

            if (state.Options.PatientProfiles is { Count: > 0 }
                || state.Options.ImportedPatientIds.Count > 0
                || state.Options.ImportedPatientBundles.Count > 0)
            {
                var profiles = state.Options.PatientProfiles;
                var selectedMeasures = (IReadOnlyList<ProfiledMeasureType>)state.Options.SelectedMeasures;
                var qualAllCount = profiles.Count(p => p.QualifiesForAll(selectedMeasures));
                var nqAllCount = profiles.Count(p => p.QualifiesForNone(selectedMeasures));
                var mixedCount = profiles.Count - qualAllCount - nqAllCount;
                var importedTotal = state.Options.ImportedPatientIds.Count + state.Options.ImportedPatientBundles.Count;
                output.WriteLine($"Using measure-eligibility profiles: {qualAllCount} qualifying-all, {nqAllCount} non-qualifying-all, {mixedCount} mixed" +
                                 (importedTotal > 0 ? $" + {importedTotal} imported patient(s)" : string.Empty));

                var importedPatients = new List<ImportedPatientInput>(
                    state.Options.ImportedPatientIds.Count + state.Options.ImportedPatientBundles.Count);
                importedPatients.AddRange(state.Options.ImportedPatientIds);
                importedPatients.AddRange(state.Options.ImportedPatientBundles);

                // Pre-load imported patient FHIR data so the pipeline can reuse it without
                // re-fetching, and surface — but do NOT enforce — whether each imported
                // encounter sits inside the scenario's configured reporting period. A
                // mismatched scenario is a legitimate test case (proper disqualification by
                // measure-eval); the run continues either way.
                if (importedPatients.Count > 0)
                {
                    output.WriteLine($"Pre-loading {importedPatients.Count} imported patient(s) (report period [{scenarioConfig.StartDate} ? {scenarioConfig.EndDate}])...");
                    await ImportedPatientLoader.LoadAllAsync(fhirDataLoader, importedPatients, output, state.RunCancellation.Token);

                    var (impStart, impEnd) = ImportedPatientLoader.ComputeEncounterDateRange(importedPatients);
                    if (impStart.HasValue || impEnd.HasValue)
                    {
                        var periodStart = TryParseUtc(scenarioConfig.StartDate);
                        var periodEnd = TryParseUtc(scenarioConfig.EndDate);

                        var beforeStart = periodStart.HasValue && impStart.HasValue && impStart.Value < periodStart.Value;
                        var afterEnd = periodEnd.HasValue && impEnd.HasValue && impEnd.Value > periodEnd.Value;

                        if (beforeStart || afterEnd)
                        {
                            output.WriteLine($"  WARNING: Imported encounter dates [{impStart:yyyy-MM-dd} ? {impEnd:yyyy-MM-dd}] fall " +
                                             $"{(beforeStart ? "before" : "")}{(beforeStart && afterEnd ? "/" : "")}{(afterEnd ? "after" : "")} " +
                                             "the configured Report Period. Affected resources will be filtered by measure-eval / CQL " +
                                             "and may cause the patient to be classified non-qualifying.");
                        }
                        else
                        {
                            output.WriteLine($"  Imported encounter dates [{impStart:yyyy-MM-dd} ? {impEnd:yyyy-MM-dd}] sit inside the report period.");
                        }
                    }
                }

                // Use the streaming pipeline: generate ? upload ? dispose per patient.
                // The pipeline builds the manifest incrementally and runs acquisition
                // simulation per-patient, so no serialized FHIR JSON is retained.
                var pipelineResult = await FhirGenerationPipeline.GenerateAndUploadAsync(
                    output,
                    fhirDataLoader,
                    selectedMeasures,
                    profiles,
                    state.Options.ResourcesPerPatient,
                    state.Options.Seed,
                    generationConfig,
                    acquisitionSimulation: new FhirGenerationPipeline.AcquisitionSimulationConfig
                    {
                        QueryPlan = effectiveQueryPlan,
                        ClinicalPeriodStart = scenarioConfig.StartDate,
                        ClinicalPeriodEnd = scenarioConfig.EndDate
                    },
                    importedPatients: importedPatients.Count > 0 ? importedPatients : null);

                patientIds = pipelineResult.PatientIds;
                generationManifest = pipelineResult.Manifest;

                // Manifest preserves order: generated profiles first, imported patients appended.
                // Use the manifest's parallel PatientIds + Profiles arrays as the source of truth.
                expectedSubmittedPatientIds = generationManifest.PatientIds
                    .Where((_, idx) => idx < generationManifest.Profiles.Count
                                       && generationManifest.Profiles[idx].QualifiesForAny(selectedMeasures))
                    .ToList();
            }
            else
            {
                var (genPatientIds, bundles) = FhirBundleGenerator.Generate(
                    output, state.Options.PatientCount, state.Options.ResourcesPerPatient, state.Options.Seed,
                    generationConfig);

                patientIds = genPatientIds;
                expectedSubmittedPatientIds = patientIds.ToList();

                await fhirDataLoader.LoadTransactionBundlesFromJsonAsync(output, bundles);
            }

            if (scenarioConfig.PatientIds.Count == 0)
                scenarioConfig.PatientIds = patientIds;

            var expectedAllPatientIds = scenarioConfig.PatientIds;

            await validationHelper.InitializeArtifactsAsync();
            await validationHelper.InitializeCategoriesAsync();

            var measureLoader = new MeasureLoader(measureEvalClient, sdkValidationClient, output, scenarioConfig);
            await measureLoader.LoadAllAsync();
            var measureIds = measureLoader.MeasureIds;
            if (measureIds.Count == 0)
                throw new InvalidOperationException("MeasureLoader did not produce any MeasureIds");
            var measureId = measureIds[0];

            var facilityId = state.RunId.ToString();
            state.FacilityId = facilityId;

            // Finalize manifest metadata now that we have measure IDs and query plan.
            if (generationManifest != null)
            {
                generationManifest.MeasureIds = measureIds;
                generationManifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(effectiveQueryPlan);
                generationManifest.ParameterQueryResourceTypes = QueryPlanDefaults.GetParameterQueryResourceTypes(effectiveQueryPlan);
                generationManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(state.Options.SelectedMeasures);

                // Persist a lightweight manifest snapshot for the UI.
                await _snapshotStore.SetDomainAsync(state.RunId, "generationManifest", generationManifest.ToSnapshot(), cancellationToken);
            }

            await FacilitySetupHelper.EnsureFacilityAsync(
                services.GetRequiredService<IFacilityServiceClient>(),
                output, facilityId, measureIds);
            await EnsureNormalizationFromSuiteAsync(
                services.GetRequiredService<INormalizationServiceClient>(),
                output, facilityId, state.Options.NormalizationSuiteId, cancellationToken);
            await FacilitySetupHelper.EnsureQueryPlansAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output, facilityId, measureIds, "Epic", queryPlanInput);
            await FacilitySetupHelper.EnsureQueryConfigAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<AutomationConfig>(),
                output, facilityId);
            await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
                services.GetRequiredService<IQueryDispatchServiceClient>(),
                output,
                facilityId);

            var reportId = await reportHelper.GenerateReportAsync(facilityId, measureIds, scenarioConfig);
            lock (state.Sync)
            {
                state.ReportId = reportId;
            }

            // Register with orchestrator so store-backed pollers start automatically.
            await _orchestrator.RegisterRunAsync(state.RunId, facilityId, reportId);

            var diagnosticsPollInterval = scenarioConfig.PatientIds.Count >= 500
                ? TimeSpan.FromSeconds(15)
                : TimeSpan.FromSeconds(5);

            await using (var diagnostics = new BackgroundDiagnosticsMonitor(
                output,
                lokiScraper,
                _automationConfig,
                scenarioConfig.PatientIds.Count,
                pollInterval: diagnosticsPollInterval,
                forwardInternalLogsToOutput: true,
                pipelineReader: services.GetRequiredService<PipelineDataReader>()))
            {
                await diagnostics.StartAsync(facilityId, reportId);
                var submitted = await reportHelper.CheckSubmissionStatusAsync(reportId, scenarioConfig, diagnostics);
                await diagnostics.StopAsync();

                if (!submitted)
                    throw new InvalidOperationException($"Expected report with id {reportId} to be submitted but it was not.");
            }

            // RegenerateReport: the first report is just a prerequisite.
            // Now trigger regeneration and track the *new* report through the full pipeline.
            if (state.Options.ReportMethod == ReportMethod.RegenerateReport)
            {
                output.WriteLine("---------------------------------------------------------------");
                output.WriteLine("Initial report submitted. Beginning REGENERATION phase...");
                output.WriteLine("---------------------------------------------------------------");

                // Flush stale domain data so the regenerated report starts fresh.
                services.GetRequiredService<PipelineDataReader>().InvalidateCache();

                var regeneratedReportId = await reportHelper.RegenerateReportAsync(facilityId, reportId);
                reportId = regeneratedReportId;
                lock (state.Sync)
                {
                    state.ReportId = reportId;
                }

                // Re-register the run with the new report ID so pollers track the regenerated report.
                await _orchestrator.UpdateRunAsync(state.RunId, facilityId, reportId, cancellationToken);
                await callbacks.PersistRunSummary();

                output.WriteLine($"Tracking regenerated report: {reportId}");

                await using var regenDiagnostics = new BackgroundDiagnosticsMonitor(
                    output,
                    lokiScraper,
                    _automationConfig,
                    scenarioConfig.PatientIds.Count,
                    pollInterval: diagnosticsPollInterval,
                    forwardInternalLogsToOutput: true,
                    pipelineReader: services.GetRequiredService<PipelineDataReader>(),
                    expectsDataAcquisition: false);

                await regenDiagnostics.StartAsync(facilityId, reportId);
                var regenSubmitted = await reportHelper.CheckSubmissionStatusAsync(reportId, scenarioConfig, regenDiagnostics);
                await regenDiagnostics.StopAsync();

                if (!regenSubmitted)
                    throw new InvalidOperationException($"Expected regenerated report with id {reportId} to be submitted but it was not.");

                output.WriteLine("Regenerated report submitted successfully.");
            }

            await pipelineSnapshot.WriteFullSnapshotAsync(output, facilityId, reportId);

            var downloadedResources = await reportHelper.DownloadReportAsync(facilityId, reportId, scenarioConfig);
            var internalAbsResources = await reportHelper.DownloadReportAsync(facilityId, reportId, scenarioConfig, external: false);

            output.WriteLine($"External manifest suppression (ExternalBlobStorage:SuppressManifest) = {_suppressExternalManifest}.");

            // Capture a lightweight ABS upload snapshot for the manifest detail page.
            try
            {
                var absSnapshot = AbsUploadSnapshot.Build(internalAbsResources);
                await _snapshotStore.SetDomainAsync(state.RunId, "absUpload", absSnapshot, cancellationToken);
            }
            catch (Exception absEx)
            {
                output.WriteLine($"[WARN] Failed to build/store ABS upload snapshot: {absEx.Message}");
            }

            // Persist ABS export locator snapshot metadata so diagnostics export can
            // locate and re-download ABS artifacts later without storing raw ABS file
            // contents or full payloads in snapshot storage.
            // Best-effort: a persistence failure must not abort the run.
            try
            {
                var absExportLocator = AbsExportLocatorSnapshot.Build(facilityId, reportId, internalAbsResources);
                await _snapshotStore.SetDomainAsync(state.RunId, "absExportLocator", absExportLocator, cancellationToken);
            }
            catch (Exception absFilesEx)
            {
                output.WriteLine($"[WARN] Failed to persist ABS export locator metadata: {absFilesEx.Message}");
            }

            static bool HasExternalFile(IReadOnlyDictionary<string, object> files, string expectedFileName, out string? matchedKey)
            {
                if (files.ContainsKey(expectedFileName))
                {
                    matchedKey = expectedFileName;
                    return true;
                }

                var suffix = "_" + expectedFileName;
                matchedKey = files.Keys.FirstOrDefault(k =>
                    k.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                return matchedKey != null;
            }

            if (!HasExternalFile(downloadedResources, "manifest.ndjson", out var manifestKey))
            {
                if (_suppressExternalManifest)
                {
                    output.WriteLine("manifest.ndjson is not present in external submission package because ExternalBlobStorage:SuppressManifest=true; continuing.");
                }
                else
                {
                    throw new InvalidOperationException("Expected report to include manifest.ndjson but it was not");
                }
            }
            else if (!string.Equals(manifestKey, "manifest.ndjson", StringComparison.Ordinal))
            {
                output.WriteLine($"Found external manifest using flattened name '{manifestKey}'.");
            }

            foreach (var patientId in expectedSubmittedPatientIds)
            {
                var expectedPatientFile = $"patient-{patientId}.ndjson";
                if (!HasExternalFile(downloadedResources, expectedPatientFile, out var patientFileKey))
                    throw new InvalidOperationException($"Expected report to include patient-{patientId}.ndjson but it was not");

                if (!string.Equals(patientFileKey, expectedPatientFile, StringComparison.Ordinal))
                    output.WriteLine($"Found external patient file using flattened name '{patientFileKey}'.");
            }

            // Flush stale cache from diagnostics polling so validators read authoritative data.
            services.GetRequiredService<PipelineDataReader>().InvalidateCache();

            // Regeneration reuses prior data acquisition — no new DA logs exist for the regenerated report.
            var expectDataAcquisitionData = state.Options.ReportMethod != ReportMethod.RegenerateReport;

            // Pipeline-built manifest already has all metadata; no need to re-parse bundles.
            // For non-profile runs (no pipeline), generationManifest remains null.

            var validatorResults = new List<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot>();

            async Task RunValidator(string name, Func<Task> action)
            {
                try
                {
                    await action();
                    validatorResults.Add(new PipelineSummarySnapshotBuilder.ValidatorResultSnapshot
                    {
                        Name = name,
                        Outcome = "Passed",
                        IssueCount = 0
                    });
                }
                catch (Exception ex)
                {
                    // Extract issue count from the conventional message format:
                    // "... failed with N issue(s)."
                    var issueCount = 0;
                    var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"(\d+)\s+issue\(s\)");
                    if (match.Success)
                        int.TryParse(match.Groups[1].Value, out issueCount);

                    validatorResults.Add(new PipelineSummarySnapshotBuilder.ValidatorResultSnapshot
                    {
                        Name = name,
                        Outcome = "Failed",
                        IssueCount = issueCount
                    });
                    // Re-throw so the run still fails as before.
                    throw;
                }
                finally
                {
                    // Persist after each validator so partial results are visible
                    // in the dashboard even if a later validator throws.
                    await _snapshotStore.SetDomainAsync(state.RunId, "validatorResults", validatorResults, cancellationToken);
                }
            }

            await RunValidator("REPORT INTERNAL ABS MANIFEST VALIDATION", () =>
                reportAbsValidator.ValidateAllAsync(
                    internalAbsResources,
                    expectedSubmittedPatientIds,
                    measureIds,
                    scenarioConfig.StartDate,
                    scenarioConfig.EndDate,
                    facilityId,
                    reportId,
                    generatedBundles: null,
                    expectedManifestPatientListIds: expectedAllPatientIds,
                    expectDataAcquisitionData: expectDataAcquisitionData,
                    manifest: generationManifest));

            // The ABS manifest validator enriches the manifest with downstream-derived
            // predictions that are not known at generation time — most notably the
            // per-patient OperationOutcome count (one OO is appended to the ABS blob for
            // every patient whose ReportingStatus is FailedValidation). Re-persist the
            // snapshot so the Runs dashboard shows the final, fully-enriched predictions
            // rather than the pre-validation snapshot taken at line ~506.
            if (generationManifest != null)
            {
                await _snapshotStore.SetDomainAsync(state.RunId, "generationManifest", generationManifest.ToSnapshot(), cancellationToken);
            }

            await RunValidator("REPORT DATABASE VALIDATION", () =>
                reportValidator.ValidateAllAsync(
                    facilityId,
                    reportId,
                    measureIds,
                    expectedAllPatientIds,
                    expectedSubmittedPatientIds: expectedSubmittedPatientIds,
                    manifest: generationManifest));

            await RunValidator("DATA ACQUISITION DATABASE VALIDATION", () =>
                dataAcqValidator.ValidateAllAsync(facilityId, reportId, measureIds[0], expectedAllPatientIds, expectDataAcquisitionData: expectDataAcquisitionData));

            await RunValidator("NORMALIZATION DATABASE VALIDATION", () =>
                normalizationValidator.ValidateAllAsync(facilityId));

            await RunValidator("TENANT DATABASE VALIDATION", () =>
                tenantValidator.ValidateAllAsync(facilityId, measureId));

            await RunValidator("VALIDATION RESULTS (API)", () =>
                validationResultsValidator.ValidateAllAsync(facilityId, reportId, expectedAllPatientIds, scenarioConfig.LokiScrapeWindow));

            await RunCleanupHelper.CleanupAfterRunAsync(
                scenarioConfig,
                services.GetRequiredService<IFacilityServiceClient>(),
                services.GetRequiredService<INormalizationServiceClient>(),
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<IQueryDispatchServiceClient>(),
                services.GetRequiredService<IReportServiceClient>(),
                fhirDataLoader,
                output,
                facilityId,
                reportId);

            if (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
                throw new OperationCanceledException("Run was cancelled.");

            state.Status = AutomationRunStatus.Succeeded;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await _orchestrator.CompleteRunAsync(state.RunId);
            await callbacks.BroadcastStatus();
            output.WriteLine("Run completed successfully.");
        }
        catch (OperationCanceledException) when (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
        {
            _logger.LogInformation("Run {RunId} cancellation acknowledged.", state.RunId);
        }
        catch (Exception ex)
        {
            if (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
            {
                _logger.LogInformation(ex, "Run {RunId} faulted after cancel request: {ExceptionType}", state.RunId, ex.GetType().Name);
                return;
            }

            _logger.LogError(ex, "Run {RunId} failed", state.RunId);
            state.Status = AutomationRunStatus.Failed;
            state.Error = ex.Message;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await _orchestrator.CompleteRunAsync(state.RunId);
            await callbacks.BroadcastStatus();
            output.WriteLine($"Run failed: {ex.Message}");
        }
    }

    private static FhirGenerationConfig ResolveFhirGenerationConfig(AutomationConfig automationConfig)
    {
        var includeLowValueOptionalReferences = automationConfig.FhirGeneration?.IncludeLowValueOptionalReferences ?? true;
        var distribution = automationConfig.FhirGeneration?.ResourceDistribution;
        if (distribution == null || distribution.Count == 0)
            return new FhirGenerationConfig
            {
                IncludeLowValueOptionalReferences = includeLowValueOptionalReferences
            };

        return new FhirGenerationConfig
        {
            IncludeLowValueOptionalReferences = includeLowValueOptionalReferences,
            ResourceDistribution = new Dictionary<string, double>(distribution, StringComparer.OrdinalIgnoreCase)
        };
    }

    private ServiceProvider BuildRunServiceProvider(IAutomationOutput output)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_automationConfig);
        services.AddSingleton(output);

        // Forward host-level configuration into the per-run container
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<ServiceRegistry>>());
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>>());
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<LinkTokenServiceSettings>>());
        services.AddSingleton(_hostServices.GetRequiredService<ICreateSystemToken>());

        services.AddLinkSdk();

        services.AddSingleton(sp => new LokiScraper(sp.GetRequiredService<IAutomationOutput>(), sp.GetRequiredService<AutomationConfig>()))
            .AddSingleton(sp => {
                var cfg = sp.GetRequiredService<AutomationConfig>();
                return new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);
            })
            .AddSingleton<PipelineDataReader>();

        services.AddTransient<ValidationApiHelper>();
        services.AddTransient<ReportApiHelper>();
        services.AddTransient<ReportDatabaseValidator>();
        services.AddTransient<ReportAbsManifestValidator>();
        services.AddTransient<DataAcquisitionDatabaseValidator>();
        services.AddTransient<NormalizationDatabaseValidator>();
        services.AddTransient<TenantDatabaseValidator>();
        services.AddTransient<ValidationResultsValidator>();
        services.AddTransient<PipelineSnapshot>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Parses a FHIR/scenario UTC timestamp. Returns null when the input is empty
    /// or unparseable. Used by the imported-patient warning when the imported
    /// encounter dates fall outside the configured report period.
    /// </summary>
    private static DateTime? TryParseUtc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dto))
            return dto.UtcDateTime;
        return null;
    }

    /// <summary>
    /// Resolves the normalization suite and creates the appropriate operations and sequences
    /// via the Normalization API for the given facility. Replaces the legacy
    /// <c>FacilitySetupHelper.EnsureNormalizationConfigAsync</c> which only created a single
    /// hard-coded CopyProperty operation.
    /// </summary>
    private async Task EnsureNormalizationFromSuiteAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId,
        Guid? suiteId,
        CancellationToken cancellationToken)
    {
        // Check if the facility already has operations configured.
        var existingResp = await normalizationClient.SearchFacilityOperationsAsync(facilityId);
        if (existingResp.IsSuccessStatusCode && existingResp.Body?.Records?.Count > 0)
        {
            output.WriteLine($"Normalization config for facility '{facilityId}' already exists. Skipping create.");
            return;
        }

        var resolution = await _normalizationSuiteResolver.ResolveAsync(suiteId, cancellationToken);
        output.WriteLine($"Using normalization suite: {resolution.SuiteName} ({resolution.Operations.Count} operation(s))");

        if (resolution.Operations.Count == 0)
        {
            output.WriteLine("Normalization suite has no operations — skipping normalization configuration.");
            return;
        }

        // Track created operation IDs per resource type so we can build sequences.
        var createdOpsByResourceType = new Dictionary<string, List<(Guid OpId, int Order)>>(StringComparer.OrdinalIgnoreCase);
        int globalOrder = 0;

        foreach (var opDef in resolution.Operations)
        {
            globalOrder++;
            var apiOp = new CreateNormalizationOperationDetailsApiModel
            {
                OperationType = opDef.OperationType,
                Name = opDef.Name,
                Description = opDef.Description ?? string.Empty,
                SourceFhirPath = opDef.SourceFhirPath ?? string.Empty,
                TargetFhirPath = opDef.TargetFhirPath ?? string.Empty
            };

            // Populate type-specific fields.
            switch (opDef.OperationType)
            {
                case "ConditionalTransform":
                    apiOp.TargetFhirPath = opDef.ConditionTargetFhirPath ?? string.Empty;
                    apiOp.TargetValue = opDef.ConditionTargetValue;
                    apiOp.Conditions = opDef.Conditions.Select(c => new CreateNormalizationConditionApiModel
                    {
                        FhirPathSource = c.FhirPathSource,
                        Operator = c.Operator,
                        Value = c.Value
                    }).ToList();
                    break;
                case "CodeMap":
                    apiOp.FhirPath = opDef.CodeMapFhirPath;
                    apiOp.CodeSystemMaps = opDef.CodeSystemMaps.Select(csm => new CreateNormalizationCodeSystemMapApiModel
                    {
                        SourceSystem = csm.SourceSystem,
                        TargetSystem = csm.TargetSystem,
                        CodeMaps = csm.CodeMaps.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new CreateNormalizationCodeMapEntryApiModel { Code = kvp.Value.Code, Display = kvp.Value.Display })
                    }).ToList();
                    break;
                case "RemoveExtensions":
                    apiOp.ExtensionUrls = opDef.ExtensionUrls;
                    break;
            }

            var createResp = await normalizationClient.CreateOperationAsync(new CreateNormalizationOperationRequestApiModel
            {
                ResourceTypes = opDef.ResourceTypes,
                FacilityId = facilityId,
                Operation = apiOp,
                Description = opDef.Description ?? string.Empty,
                VendorIds = []
            }, cancellationToken);

            if (!createResp.IsSuccessStatusCode)
            {
                output.WriteLine($"  WARNING: Failed to create normalization operation '{opDef.Name}': HTTP {createResp.StatusCode}");
                continue;
            }

            output.WriteLine($"  Created operation: {opDef.Name} ({opDef.OperationType}) for [{string.Join(", ", opDef.ResourceTypes)}]");

            // Track for sequence creation.
            foreach (var rt in opDef.ResourceTypes)
            {
                if (!createdOpsByResourceType.ContainsKey(rt))
                    createdOpsByResourceType[rt] = [];
                createdOpsByResourceType[rt].Add((opDef.Id, globalOrder));
            }
        }

        // Create sequences per resource type.
        // We need to get operations back from the API since the create response may not give IDs directly.
        // Instead, re-search to find newly created ops and build sequences.
        var opsResp = await normalizationClient.SearchFacilityOperationsAsync(facilityId, pageSize: 100, cancellationToken: cancellationToken);
        if (opsResp.IsSuccessStatusCode && opsResp.Body?.Records?.Count > 0)
        {
            // Group by resource type and create sequences in order.
            var opsByResourceType = new Dictionary<string, List<(Guid OpId, int Seq)>>(StringComparer.OrdinalIgnoreCase);
            int seq = 0;
            foreach (var op in opsResp.Body.Records)
            {
                seq++;
                foreach (var ort in op.OperationResourceTypes)
                {
                    var resourceName = ort.Resource?.ResourceName;
                    if (string.IsNullOrEmpty(resourceName)) continue;

                    if (!opsByResourceType.ContainsKey(resourceName))
                        opsByResourceType[resourceName] = [];
                    opsByResourceType[resourceName].Add((op.Id, seq));
                }
            }

            foreach (var (resourceType, ops) in opsByResourceType)
            {
                var sequences = ops
                    .OrderBy(o => o.Seq)
                    .Select((o, idx) => new CreateNormalizationOperationSequenceApiModel
                    {
                        OperationId = o.OpId,
                        Sequence = idx + 1
                    })
                    .ToList();

                var seqResp = await normalizationClient.CreateOperationSequencesAsync(facilityId, resourceType, sequences, cancellationToken);
                if (seqResp.IsSuccessStatusCode)
                    output.WriteLine($"  Created operation sequence for resource type: {resourceType} ({sequences.Count} op(s))");
                else
                    output.WriteLine($"  WARNING: Failed to create sequence for {resourceType}: HTTP {seqResp.StatusCode}");
            }
        }
    }
}
