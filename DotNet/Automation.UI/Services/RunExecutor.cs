using Automation.UI.Models;
using LantanaGroup.Automation;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using Microsoft.Extensions.Options;
using Task = System.Threading.Tasks.Task;

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
    private const ScheduledInpatientPattern DefaultScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;

    private readonly AutomationConfig _automationConfig;
    private readonly IServiceProvider _hostServices;
    private readonly ISnapshotStore _snapshotStore;
    private readonly RunSnapshotOrchestrator _orchestrator;
    private readonly QueryPlanTemplateResolver _queryPlanResolver;
    private readonly NormalizationSuiteResolver _normalizationSuiteResolver;
    private readonly OrganizationResourceMapTemplateResolver _organizationResourceMapResolver;
    private readonly bool _suppressExternalManifest;
    private readonly bool _includePatientAggregatorOrganizationResource;
    private readonly string _includePatientAggregatorOrganizationResourceSource;
    private readonly ILogger _logger;

    public RunExecutor(
        AutomationConfig automationConfig,
        IServiceProvider hostServices,
        ISnapshotStore snapshotStore,
        RunSnapshotOrchestrator orchestrator,
        QueryPlanTemplateResolver queryPlanResolver,
        NormalizationSuiteResolver normalizationSuiteResolver,
        OrganizationResourceMapTemplateResolver organizationResourceMapResolver,
        IConfiguration configuration,
        ILogger logger)
    {
        _automationConfig = automationConfig;
        _hostServices = hostServices;
        _snapshotStore = snapshotStore;
        _orchestrator = orchestrator;
        _queryPlanResolver = queryPlanResolver;
        _normalizationSuiteResolver = normalizationSuiteResolver;
        _organizationResourceMapResolver = organizationResourceMapResolver;
        _suppressExternalManifest = configuration.GetValue<bool>("ExternalBlobStorage:SuppressManifest");
        var includeOrg = ResolveIncludePatientAggregatorOrganizationResource(configuration);
        _includePatientAggregatorOrganizationResource = includeOrg.Value;
        _includePatientAggregatorOrganizationResourceSource = includeOrg.Source;
        _logger = logger;
    }

    private static (bool Value, string Source) ResolveIncludePatientAggregatorOrganizationResource(IConfiguration configuration)
    {
        var shared = TryGetBool(configuration, "PatientAggregator:IncludeOrganizationResource");
        if (shared.HasValue)
            return (shared.Value, "PatientAggregator:IncludeOrganizationResource");

        return (false, "default(false)");
    }

    private static bool? TryGetBool(IConfiguration configuration, string key)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return bool.TryParse(raw, out var parsed) ? parsed : null;
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

            var usesScheduledWorkflow = state.Options.ReportMethod is ReportMethod.ScheduledReport or ReportMethod.RegenerateReport;

            // For scheduled-style runs, compute the active window immediately so generation
            // uses the correct clinical period boundaries (scenarioConfig.StartDate/EndDate).
            if (usesScheduledWorkflow)
            {
                // Use a clinically meaningful report window for generation so scheduled
                // inpatient patterns produce rich in-period evidence (not minute-scale data).
                //
                // End-of-period runtime is kept short separately in the scheduled workflow by
                // publishing a small delay value to Admin.BFF (without changing Admin.BFF logic).
                var now = DateTimeOffset.UtcNow;
                var alignedNow = new DateTimeOffset(
                    now.Year, now.Month, now.Day,
                    now.Hour, now.Minute, 0,
                    TimeSpan.Zero);
                // 5-day clinical window gives scheduled inpatient patterns enough LOS
                // for ACH monthly IP criteria while runtime remains bounded by the
                // short scheduled delay sent to Admin.BFF.
                var start = alignedNow.AddDays(-5);
                var end = alignedNow.AddMinutes(2);
                scenarioConfig.StartDate = ToZulu(start);
                scenarioConfig.EndDate = ToZulu(end);
            }

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
            var normalizationSuiteApplicationValidator = new NormalizationSuiteApplicationValidator(output);
            var tenantValidator = services.GetRequiredService<TenantDatabaseValidator>();
            var validationResultsValidator = services.GetRequiredService<ValidationResultsValidator>();
            var pipelineSnapshot = services.GetRequiredService<PipelineSnapshot>();

            output.WriteLine($"Starting {state.Scenario} run: {state.RunId}");
            output.WriteLine($"Measure context: {string.Join(", ", state.Options.SelectedMeasures.Select(m => $"{ProfiledMeasureCatalog.GetDisplayName(m)} ({m})"))}");
            output.WriteLine($"NHSN Organization ID: {state.Options.NhsnOrganizationId}");
            output.WriteLine($"Generation config: patients={state.Options.PatientCount}, resourcesPerPatient={state.Options.ResourcesPerPatient}, seed={state.Options.Seed}");

            List<string> patientIds;
            List<string> expectedSubmittedPatientIds;
            GenerationManifest? generationManifest = null;

            // Use the first measure for generation context (profile-driven generation picks
            // the most restrictive measure — patients qualifying for all measures must meet
            // the criteria of each). For multi-measure, the pipeline handles the union.
            var primaryMeasure = state.Options.SelectedMeasures[0];
            var generationConfig = ResolveFhirGenerationConfig(_automationConfig);
            var normalizationResolution = await _normalizationSuiteResolver.ResolveAsync(state.Options.NormalizationSuiteId, cancellationToken);
            var organizationResourceMapTemplate = await _organizationResourceMapResolver.ResolveAsync(state.Options.OrganizationResourceMapTemplateId, cancellationToken);
            var generationRequirementsPlan = BuildGenerationRequirementsPlan(normalizationResolution, organizationResourceMapTemplate);

            await fhirDataLoader.WaitForServerAsync(output);

            // Resolve the query plan template early so the acquisition simulator uses the
            // same plan the scenario is configured with (not always the built-in default).
            var queryPlanResolution = await _queryPlanResolver.ResolveAsync(state.Options.QueryPlanTemplateId, cancellationToken);
            var queryPlanInput = queryPlanResolution.Input;
            var effectiveQueryPlan = queryPlanInput ?? QueryPlanDefaults.GetDefaultAsInput();
            if (!string.IsNullOrWhiteSpace(queryPlanResolution.Name))
                output.WriteLine($"Using query plan: {queryPlanResolution.Name}");

            var locationQueryCount = effectiveQueryPlan.InitialQueries.Concat(effectiveQueryPlan.SupplementalQueries)
                .Count(q => string.Equals(q.ResourceType, "Location", StringComparison.OrdinalIgnoreCase));
            var expectLocationResources = locationQueryCount > 0;
            var encounterQueryCount = effectiveQueryPlan.InitialQueries.Concat(effectiveQueryPlan.SupplementalQueries)
                .Count(q => string.Equals(q.ResourceType, "Encounter", StringComparison.OrdinalIgnoreCase));
            var expectEncounterResources = encounterQueryCount > 0;
            output.WriteLine($"Query plan Location queries: {locationQueryCount}");
            output.WriteLine($"Query plan Encounter queries: {encounterQueryCount}");
            if (locationQueryCount == 0)
                output.WriteLine("WARNING: Query plan has no Location query entries; org-location mapping cannot be exercised for this run.");
            if (encounterQueryCount == 0)
                output.WriteLine("WARNING: Query plan has no Encounter query entries; encounter mapping checks will be limited for this run.");

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
                    generationRequirementsPlan,
                    acquisitionSimulation: new FhirGenerationPipeline.AcquisitionSimulationConfig
                    {
                        QueryPlan = effectiveQueryPlan,
                        ClinicalPeriodStart = scenarioConfig.StartDate,
                        ClinicalPeriodEnd = scenarioConfig.EndDate,
                        OrganizationLocationConditionFhirPaths = organizationResourceMapTemplate?.Conditions
                            ?.Where(c => !string.IsNullOrWhiteSpace(c.FhirPath))
                            .OrderBy(c => c.Priority)
                            .Select(c => NormalizeOrgLocationFhirPathForDataAcquisition(c.FhirPath))
                            .ToList(),
                        AllowEncounterAnchoredDateOverrideForOutOfRange =
                            state.Options.ReportMethod == ReportMethod.ScheduledReport
                            || state.Options.ReportMethod == ReportMethod.RegenerateReport
                    },
                    importedPatients: importedPatients.Count > 0 ? importedPatients : null);

                patientIds = pipelineResult.PatientIds;
                generationManifest = pipelineResult.Manifest;

                // Manifest preserves order: generated profiles first, imported patients appended.
                // Use the manifest's parallel PatientIds + Profiles arrays as the source of truth.
                expectedSubmittedPatientIds = generationManifest.PatientIds
                    .Where((_, idx) => idx < generationManifest.Profiles.Count
                                       && generationManifest.Profiles[idx].IsExpectedToBeSubmitted(selectedMeasures))
                    .ToList();

                if (state.Options.ReportMethod == ReportMethod.ScheduledReport)
                {
                    expectedSubmittedPatientIds = ComputeExpectedScheduledSubmittedPatientIds(
                        generationManifest.PatientIds,
                        generationManifest.Profiles,
                        selectedMeasures);
                }
            }
            else
            {
                var (genPatientIds, bundles) = FhirBundleGenerator.Generate(
                    output, state.Options.PatientCount, state.Options.ResourcesPerPatient, state.Options.Seed,
                    generationConfig,
                    generationRequirementsPlan);

                patientIds = genPatientIds;
                expectedSubmittedPatientIds = patientIds.ToList();

                await fhirDataLoader.LoadTransactionBundlesFromJsonAsync(output, bundles);
            }

            if (scenarioConfig.PatientIds.Count == 0)
                scenarioConfig.PatientIds = patientIds;

            var expectedAllPatientIds = scenarioConfig.PatientIds;
            var expectedReportEntryPatientIds = expectedAllPatientIds.ToList();
            IReadOnlyCollection<string> expectedManifestPatientListIds = expectedAllPatientIds;

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
                generationManifest.IncludePatientAggregatorOrganizationResource = _includePatientAggregatorOrganizationResource;
                output.WriteLine($"[Manifest] IncludePatientAggregatorOrganizationResource={_includePatientAggregatorOrganizationResource} (source={_includePatientAggregatorOrganizationResourceSource})");

                // Persist a lightweight manifest snapshot for the UI.
                await _snapshotStore.SetDomainAsync(state.RunId, "generationManifest", generationManifest.ToSnapshot(), cancellationToken);
            }

            await FacilitySetupHelper.EnsureFacilityAsync(
                services.GetRequiredService<IFacilityServiceClient>(),
                output, facilityId, measureIds);
            normalizationResolution = await EnsureNormalizationFromSuiteAsync(
                services.GetRequiredService<INormalizationServiceClient>(),
                output, facilityId, state.Options.NormalizationSuiteId, cancellationToken, normalizationResolution);
            await FacilitySetupHelper.EnsureQueryPlansAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output, facilityId, measureIds, "Epic", queryPlanInput);
            await FacilitySetupHelper.EnsureQueryConfigAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<AutomationConfig>(),
                output, facilityId);
            await EnsureOrganizationLocationConfigurationFromTemplateAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output,
                facilityId,
                organizationResourceMapTemplate,
                cancellationToken);
            await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
                services.GetRequiredService<IQueryDispatchServiceClient>(),
                output,
                facilityId);
            await WriteOrganizationLocationMappingStatusAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output,
                facilityId,
                cancellationToken);

            // Census config + FHIR list config are required so the Census service accepts the
            // explicit PatientListsAcquired snapshots this workflow publishes (ProcessList
            // rejects facilities without a census config).
            //
            // For Automation runs, disable the background census Quartz job for all report
            // methods. The automation workflow drives patient-list ingestion explicitly when
            // needed, while the scheduled background job can attempt to read non-existent
            // synthetic FHIR List resources (census-{facility}-...) and emit noisy
            // DataAcquisition "Error retrieving patient list" exceptions that do not represent
            // true pipeline failures.
            var enableBackgroundCensusJobs = false;
            await FacilitySetupHelper.EnsureCensusConfigAsync(
                services.GetRequiredService<ICensusServiceClient>(),
                output,
                facilityId,
                enabled: enableBackgroundCensusJobs);
            await FacilitySetupHelper.EnsureFhirListConfigAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<AutomationConfig>(),
                output,
                facilityId);

            Frequency? scheduledRunFrequency = null;
            string reportId;
            string normalizationEvidenceReportId;
            if (usesScheduledWorkflow)
            {
                var scheduledWorkflowState = await ExecuteScheduledReportWorkflowAsync(
                    reportHelper,
                    output,
                    facilityId,
                    measureIds,
                    state.Options.SelectedMeasures,
                    scenarioConfig,
                    patientIds,
                    state.Options.PatientProfiles,
                    cancellationToken);

                reportId = scheduledWorkflowState.ReportTrackingId;
                normalizationEvidenceReportId = reportId;
                scheduledRunFrequency = scheduledWorkflowState.Frequency;
            }
            else
            {
                reportId = await reportHelper.GenerateReportAsync(facilityId, measureIds, scenarioConfig);
                normalizationEvidenceReportId = reportId;
            }
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

            if (usesScheduledWorkflow)
            {
                // Scheduled runs can briefly report Submitted while entry-level state is still
                // converging. Reconcile against terminal report truth (entries + submission
                // statuses) so validators and artifact expectations are aligned with reality.
                var terminalState = await reportHelper.WaitForTerminalReportStateAsync(reportId, cancellationToken: cancellationToken);

                expectedReportEntryPatientIds = terminalState.EntryPatientIds;
                expectedManifestPatientListIds = terminalState.EntryPatientIds;
                expectedSubmittedPatientIds = terminalState.SubmittedPatientIds;

                output.WriteLine(
                    $"Scheduled expected sets reconciled from terminal report state: " +
                    $"entry-patients={expectedReportEntryPatientIds.Count}, submitted-patients={expectedSubmittedPatientIds.Count}.");

                // Refresh cached pipeline reads after terminal-state reconciliation so
                // snapshots/validators use the same committed state we just polled.
                services.GetRequiredService<PipelineDataReader>().InvalidateCache();
            }

            // Scope ABS prediction to the same submitted-patient truth used by validators.
            // This prevents dashboard/manifest prediction from counting non-submitted
            // patients (e.g., scheduled cohorts outside report inclusion).
            if (generationManifest != null)
            {
                generationManifest.ExpectedAbsPatientIdsOverride =
                    new HashSet<string>(expectedSubmittedPatientIds, StringComparer.Ordinal);
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

                var originalReportId = reportId;
                var regeneratedReportId = await reportHelper.RegenerateReportAsync(facilityId, reportId);
                reportId = regeneratedReportId;
                normalizationEvidenceReportId = originalReportId;
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

            await pipelineSnapshot.WriteFullSnapshotAsync(
                output,
                facilityId,
                reportId,
                BuildNormalizationSuiteSnapshot(normalizationResolution));

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

            // Failures are collected and re-thrown together once every validator has run — see
            // ValidatorRunner for why failing on the first one destroyed the evidence needed to
            // localise a discrepancy. Results are persisted after each validator so partial results
            // stay visible in the dashboard even when a later validator fails.
            var validatorRunner = new ValidatorRunner((results, ct) =>
                _snapshotStore.SetDomainAsync(state.RunId, "validatorResults", results, ct));

            Task RunValidator(string name, Func<Task> action) =>
                validatorRunner.RunAsync(name, action, cancellationToken);

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
                    expectedManifestPatientListIds: expectedManifestPatientListIds,
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
                    expectedReportEntryPatientIds,
                    expectedFrequency: state.Options.ReportMethod == ReportMethod.ScheduledReport
                        ? (scheduledRunFrequency ?? throw new InvalidOperationException("Scheduled run frequency was not captured from scheduled workflow state."))
                        : Frequency.Adhoc,
                    expectedAdHocType: state.Options.ReportMethod == ReportMethod.ScheduledReport ? null : "Manual",
                    expectedSubmittedPatientIds: expectedSubmittedPatientIds,
                    manifest: generationManifest));

            await RunValidator("DATA ACQUISITION DATABASE VALIDATION", () =>
                dataAcqValidator.ValidateAllAsync(
                    facilityId,
                    reportId,
                    measureIds[0],
                    state.Options.ReportMethod == ReportMethod.ScheduledReport
                        ? expectedReportEntryPatientIds
                        : expectedAllPatientIds,
                    expectDataAcquisitionData: expectDataAcquisitionData,
                    expectLocationResources: expectLocationResources,
                    expectEncounterResources: expectEncounterResources));

            await RunValidator("NORMALIZATION DATABASE VALIDATION", () =>
                normalizationValidator.ValidateAllAsync(facilityId));

            var normalizationSummaryMarker = "[NormalizationExecutionSummary]";
            var evidenceRequiredResourceTypes = normalizationResolution.Sequences
                .SelectMany(s => s.Operations)
                .Where(s => !string.Equals(s.Operation.OperationType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase))
                .SelectMany(s => s.Operation.ResourceTypes)
                .Concat(
                    normalizationResolution.StandaloneOperations
                        .Where(o => !string.Equals(o.OperationType, "RemoveExtensions", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(o => o.ResourceTypes))
                .Where(rt => !string.IsNullOrWhiteSpace(rt))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var runScopeFilters = new List<string> { facilityId, normalizationEvidenceReportId };

            async Task<List<string>> QueryNormalizationSummaryLogsAsync()
            {
                var logs = new List<string>();

                if (evidenceRequiredResourceTypes.Count > 0)
                {
                    foreach (var resourceType in evidenceRequiredResourceTypes)
                    {
                        var logsForResourceType = await lokiScraper.QueryServiceLogsAsync(
                            LokiScraper.Components.Normalization,
                            normalizationSummaryMarker,
                            scenarioConfig.LokiScrapeWindow,
                            additionalContainsFilters: [.. runScopeFilters, resourceType],
                            limit: 5000,
                            maxPages: 20);

                        logs.AddRange(logsForResourceType);
                    }
                }
                else
                {
                    logs = await lokiScraper.QueryServiceLogsAsync(
                        LokiScraper.Components.Normalization,
                        normalizationSummaryMarker,
                        scenarioConfig.LokiScrapeWindow,
                        additionalContainsFilters: runScopeFilters,
                        limit: 5000,
                        maxPages: 20);
                }

                return logs
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }

            var normalizationSummaryLogs = await QueryNormalizationSummaryLogsAsync();
            var retryAttemptsUsed = 0;

            if (normalizationSummaryLogs.Count == 0)
            {
                const int retryAttempts = 6;
                var retryDelay = TimeSpan.FromSeconds(5);

                output.WriteLine("[Normalization Suite] No summary logs found on first attempt. Retrying for up to 30s...");

                for (var attempt = 1; attempt <= retryAttempts; attempt++)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                    normalizationSummaryLogs = await QueryNormalizationSummaryLogsAsync();
                    retryAttemptsUsed = attempt;

                    if (normalizationSummaryLogs.Count > 0)
                        break;
                }
            }

            output.WriteLine($"[Normalization Suite] Collected {normalizationSummaryLogs.Count} normalization summary log line(s) for evidence validation (firstAttempt={(retryAttemptsUsed == 0 ? normalizationSummaryLogs.Count : 0)}, retryAttempts={retryAttemptsUsed}).");

            await RunValidator("NORMALIZATION SUITE APPLICATION VALIDATION", () =>
                normalizationSuiteApplicationValidator.ValidateAllAsync(internalAbsResources, normalizationResolution, normalizationSummaryLogs));

            await RunValidator("TENANT DATABASE VALIDATION", () =>
                tenantValidator.ValidateAllAsync(facilityId, measureId));

            await RunValidator("VALIDATION RESULTS (API)", () =>
                validationResultsValidator.ValidateAllAsync(facilityId, reportId, expectedAllPatientIds, scenarioConfig.LokiScrapeWindow));

            // Thrown before cleanup, matching the previous behaviour of leaving a failed run's data in
            // place for inspection.
            validatorRunner.ThrowIfAnyFailed();

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

    private static List<string> ComputeExpectedScheduledSubmittedPatientIds(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<PatientProfile> profiles,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures)
    {
        var expected = new List<string>();
        var count = Math.Min(patientIds.Count, profiles.Count);
        for (var i = 0; i < count; i++)
        {
            var profile = profiles[i];
            if (!profile.IsExpectedToBeSubmitted(selectedMeasures))
                continue;

            expected.Add(patientIds[i]);
        }

        return expected;
    }

    private static string ToZulu(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private readonly record struct ScheduledReportWindow(DateTimeOffset Start, TimeSpan Duration);
    private readonly record struct ScheduledWorkflowState(string ReportTrackingId, Frequency Frequency);

    // Keep scheduled test runtimes bounded while still allowing long clinical windows.
    // Admin.BFF computes EndDate using current-time + delay, so this controls how long
    // the run waits for end-of-period execution.
    private static readonly TimeSpan ScheduledRuntimeDelay = TimeSpan.FromMinutes(2);

    private static ScheduledReportWindow DeriveScheduledReportWindow(TestScenarioConfig scenarioConfig)
    {
        // scenarioConfig.StartDate/EndDate have already been set by RunExecutor early-phase.
        // Parse them back and derive the duration for the scheduled workflow.
        var start = DateTimeOffset.Parse(scenarioConfig.StartDate);
        var end = DateTimeOffset.Parse(scenarioConfig.EndDate);
        var duration = end - start;
        return new ScheduledReportWindow(start, duration);
    }

    private static async Task<ScheduledWorkflowState> ExecuteScheduledReportWorkflowAsync(
        ReportApiHelper reportHelper,
        IAutomationOutput output,
        string facilityId,
        IReadOnlyList<string> measureIds,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures,
        TestScenarioConfig scenarioConfig,
        IReadOnlyList<string> patientIds,
        IReadOnlyList<PatientProfile> profiles,
        CancellationToken cancellationToken)
    {
        var window = DeriveScheduledReportWindow(scenarioConfig);

        // Resolve each patient's census behavior from its scheduled inpatient pattern.
        // ScheduledInpatientPattern.GetCensusBehavior() is the single source of truth shared
        // with validation, so orchestration and expected-submission logic can never drift.
        //   - admitDuringWindow:     every patient that participates in the report period.
        //   - remainInpatient:       admitted and never discharged (captured by the
        //                            End-of-Report-Period job at the window boundary).
        //   - dischargeDuringWindow: admitted then discharged inside the window, which drives
        //                            a QueryDispatch discharge dispatch (data acquisition).
        var admitDuringWindow = new List<string>();
        var remainInpatient = new List<string>();
        var dischargeDuringWindow = new List<string>();

        var count = Math.Min(patientIds.Count, profiles.Count);
        for (var i = 0; i < count; i++)
        {
            var pattern = profiles[i].ScheduledInpatientPattern
                ?? DefaultScheduledInpatientPattern;
            var behavior = pattern.GetCensusBehavior();

            // Patterns whose entire stay sits outside the report period emit no census events;
            // their synthetic encounter dates keep them out of the report via measure-eval.
            if (!behavior.EmitAdmitDuringWindow)
                continue;

            admitDuringWindow.Add(patientIds[i]);
            if (behavior.EmitDischargeDuringWindow)
                dischargeDuringWindow.Add(patientIds[i]);
            else
                remainInpatient.Add(patientIds[i]);
        }

        var scheduleFrequency = selectedMeasures.Contains(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            ? Frequency.Monthly
            : Frequency.Daily;

        var reportTrackingId = await reportHelper.StartScheduledReportAsync(
            facilityId,
            measureIds,
            window.Start,
            ScheduledRuntimeDelay,
            scheduleFrequency,
            reportTrackingId: Guid.NewGuid().ToString());

        output.WriteLine(
            $"Scheduled inpatient patterns resolved: admit-in-period={admitDuringWindow.Count}, " +
            $"remain-inpatient={remainInpatient.Count}, discharge-in-period={dischargeDuringWindow.Count}.");

        // The ReportScheduled event is processed asynchronously, so the schedule record is not
        // committed the instant StartScheduledReportAsync returns. Block until Report has persisted
        // it: otherwise the admit snapshot below produces PatientEvents that reach Report's
        // PatientEventListener before the schedule exists, throwing "No Scheduled Reports found".
        var persistedSchedule = await reportHelper.WaitForScheduledReportAsync(reportTrackingId, cancellationToken: cancellationToken);

        // Reconcile to persisted report-period dates (single source of truth). This keeps
        // validators aligned with real schedule boundaries even when the scheduler/broker path
        // normalizes or computes end-date differently than the original request payload.
        var persistedStartUtc = DateTime.SpecifyKind(persistedSchedule.ReportStartDate, DateTimeKind.Utc);
        var persistedEndUtc = DateTime.SpecifyKind(persistedSchedule.ReportEndDate, DateTimeKind.Utc);
        scenarioConfig.StartDate = ToZulu(new DateTimeOffset(persistedStartUtc));
        scenarioConfig.EndDate = ToZulu(new DateTimeOffset(persistedEndUtc));

        // --- Census snapshot 1: admit every in-period patient. ---
        // Each admit produces a PatientEvent that the Report service turns into a
        // PatientIdentified report entry, and Census records the encounter as admitted.
        if (admitDuringWindow.Count > 0)
        {
            output.WriteLine($"Census snapshot 1 — admitting {admitDuringWindow.Count} in-period patient(s)...");
            await reportHelper.PublishPatientListAcquiredAsync(
                facilityId,
                reportTrackingId,
                admitPatientIds: admitDuringWindow,
                dischargePatientIds: null);
        }

        // --- Census snapshot 2: keep remainers admitted, discharge the rest. ---
        if (dischargeDuringWindow.Count > 0)
        {
            // Let the admit events propagate (Census → PatientEvent → Report entry creation) so
            // every patient we are about to discharge already has an encounter and report entry.
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

            // The snapshot lists the still-admitted patients plus explicit discharges; Census
            // discharges the listed patients inside the window, which triggers the QueryDispatch
            // discharge dispatch (and therefore data acquisition) for those patients. Listing the
            // remain-inpatient patients keeps them out of Census's auto-discharge-by-omission.
            output.WriteLine($"Census snapshot 2 — discharging {dischargeDuringWindow.Count} in-period patient(s), keeping {remainInpatient.Count} inpatient...");
            await reportHelper.PublishPatientListAcquiredAsync(
                facilityId,
                reportTrackingId,
                admitPatientIds: remainInpatient,
                dischargePatientIds: dischargeDuringWindow);
        }

        // Remain-inpatient patients are intentionally left admitted; the End-of-Report-Period
        // job acquires them when the window closes.
        if (remainInpatient.Count > 0)
            output.WriteLine($"{remainInpatient.Count} patient(s) remain inpatient; they will be acquired by the end-of-report-period job.");

        return new ScheduledWorkflowState(reportTrackingId, persistedSchedule.Frequency);
    }

    private static async Task WriteOrganizationLocationMappingStatusAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var configsResp = await dataAcqClient.GetOrganizationLocationConfigurationsAsync(facilityId, cancellationToken);
            if (!configsResp.IsSuccessStatusCode)
            {
                output.WriteLine($"Org-location mapping status: unable to read configurations (HTTP {configsResp.StatusCode}).");
                return;
            }

            var configs = configsResp.Body ?? [];
            var activeConfigs = configs.Count(c => c.IsActive);
            var activeConditions = configs.Where(c => c.IsActive).Sum(c => c.Conditions?.Count ?? 0);

            var mappingsResp = await dataAcqClient.GetOrganizationLocationMappingsAsync(facilityId, cancellationToken);
            var mappings = mappingsResp.IsSuccessStatusCode
                ? mappingsResp.Body ?? []
                : [];
            var activeMappings = mappings.Count(m => m.IsActive);
            var orgMappings = mappings.Count(m => m.IsActive && m.IsOrgLocation);

            var encounterMappingsResp = await dataAcqClient.GetEncounterMappingsAsync(facilityId, cancellationToken);
            var encounterMappings = encounterMappingsResp.IsSuccessStatusCode
                ? encounterMappingsResp.Body ?? []
                : [];
            var orgEncounterMappings = encounterMappings.Count(m => m.MappedToOrg);

            output.WriteLine(
                $"Org-location mapping status: activeConfigs={activeConfigs}, activeConditions={activeConditions}, activeMappings={activeMappings}, orgMappings={orgMappings}, encounterMappings={encounterMappings.Count}, orgEncounterMappings={orgEncounterMappings}");

            if (activeConfigs == 0 || activeConditions == 0)
                output.WriteLine("  WARNING: Org-location mapping is not effectively enabled (missing active config/conditions).");
        }
        catch (Exception ex)
        {
            output.WriteLine($"Org-location mapping status: failed to query mapping state ({ex.GetType().Name}: {ex.Message}).");
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

    private static async Task EnsureOrganizationLocationConfigurationFromTemplateAsync(
        IDataAcquisitionServiceClient dataAcqClient,
        IAutomationOutput output,
        string facilityId,
        OrganizationResourceMapTemplate? template,
        CancellationToken cancellationToken)
    {
        if (template == null)
        {
            output.WriteLine($"No Organization Resource Map template resolved for facility '{facilityId}'. Skipping org-location configuration create.");
            return;
        }

        var conditions = template.Conditions
            .Where(c => !string.IsNullOrWhiteSpace(c.FhirPath))
            .OrderBy(c => c.Priority)
            .Select(c => new CreateOrganizationLocationConditionApiModel
            {
                FhirPath = NormalizeOrgLocationFhirPathForDataAcquisition(c.FhirPath),
                Priority = c.Priority
            })
            .ToList();

        if (conditions.Count == 0)
            throw new InvalidOperationException($"Organization resource map template '{template.Name}' has no valid conditions.");

        var existing = await dataAcqClient.GetOrganizationLocationConfigurationsAsync(facilityId, cancellationToken);
        if (existing.IsSuccessStatusCode && existing.Body != null)
        {
            var normalizedTemplate = string.Join("\n", conditions.Select(c => $"{c.Priority}:{c.FhirPath}"));
            var hasMatchingActive = existing.Body.Any(cfg =>
                cfg.IsActive
                && string.Join("\n", cfg.Conditions.OrderBy(c => c.Priority).Select(c => $"{c.Priority}:{c.FhirPath}")) == normalizedTemplate);

            if (hasMatchingActive)
            {
                output.WriteLine($"Org-location configuration for facility '{facilityId}' already matches template '{template.Name}'. Skipping create.");
                return;
            }
        }

        var create = await dataAcqClient.CreateOrganizationLocationConfigurationAsync(
            facilityId,
            new CreateOrganizationLocationConfigurationApiModel
            {
                Description = template.Description ?? $"Automation org-location mapping from template '{template.Name}'",
                IsActive = true,
                Conditions = conditions
            },
            cancellationToken);

        if (!create.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Failed to create organization location configuration for facility '{facilityId}' from template '{template.Name}'. HTTP {create.StatusCode}: {create.RawBody ?? "(no body)"}");

        output.WriteLine($"Ensured org-location configuration for facility '{facilityId}' from template '{template.Name}'.");
    }

    private static string NormalizeOrgLocationFhirPathForDataAcquisition(string fhirPath)
    {
        var path = (fhirPath ?? string.Empty).Trim();
        if (path.StartsWith("Location.", StringComparison.OrdinalIgnoreCase))
            return path["Location.".Length..];

        return path;
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
        services.AddHttpClient();
        services.AddHttpClient<LokiScraper>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<AutomationConfig>();
            var configuredBaseUrl = string.IsNullOrWhiteSpace(cfg.LokiBaseUrl)
                ? "http://localhost:3100"
                : cfg.LokiBaseUrl;

            if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri))
                client.BaseAddress = baseUri;
        });

        services.AddLinkSdk();

        services.AddSingleton(sp => {
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

    private static PipelineSnapshot.NormalizationSuiteSnapshot BuildNormalizationSuiteSnapshot(NormalizationSuiteResolution resolution)
    {
        var sequences = resolution.Sequences
            .Select(s => new PipelineSnapshot.NormalizationSequenceSnapshot(
                s.SequenceName,
                s.Operations
                    .OrderBy(o => o.Sequence)
                    .Select(o => new PipelineSnapshot.NormalizationSequenceOperationSnapshot(
                        o.Sequence,
                        o.Operation.OperationType,
                        o.Operation.Name,
                        o.Operation.ResourceTypes))
                    .ToList()))
            .ToList();

        var standaloneOperations = resolution.StandaloneOperations
            .Select(o => new PipelineSnapshot.NormalizationSequenceOperationSnapshot(
                Sequence: 0,
                OperationType: o.OperationType,
                OperationName: o.Name,
                ResourceTypes: o.ResourceTypes))
            .ToList();

        return new PipelineSnapshot.NormalizationSuiteSnapshot(
            resolution.SuiteName,
            sequences,
            standaloneOperations);
    }

    private static GenerationRequirementsPlan BuildGenerationRequirementsPlan(
        NormalizationSuiteResolution resolution,
        OrganizationResourceMapTemplate? organizationResourceMapTemplate)
    {
        var plan = new GenerationRequirementsPlan
        {
            PlanName = resolution.SuiteName,
            Requirements = []
        };

        foreach (var op in resolution.Operations)
        {
            plan.Requirements.Add(new GenerationRequirement
            {
                Name = op.Name,
                RequirementType = op.OperationType,
                ResourceTypes = op.ResourceTypes.ToList(),
                SourceFhirPath = op.SourceFhirPath,
                CodeMapFhirPath = op.CodeMapFhirPath,
                ExtensionUrls = op.ExtensionUrls.ToList(),
                Conditions = op.Conditions.Select(c => new GenerationRequirementCondition
                {
                    FhirPathSource = c.FhirPathSource,
                    Operator = c.Operator,
                    Value = c.Value
                }).ToList(),
                CodeSystemMaps = op.CodeSystemMaps.Select(m => new GenerationRequirementCodeSystemMap
                {
                    SourceSystem = m.SourceSystem,
                    SourceCodes = m.CodeMaps.Keys.ToDictionary(k => k, _ => string.Empty, StringComparer.Ordinal)
                }).ToList()
            });
        }

        if (organizationResourceMapTemplate?.Conditions is { Count: > 0 })
        {
            foreach (var condition in organizationResourceMapTemplate.Conditions
                         .Where(c => !string.IsNullOrWhiteSpace(c.FhirPath))
                         .OrderBy(c => c.Priority))
            {
                plan.Requirements.Add(new GenerationRequirement
                {
                    Name = $"Organization Resource Map Condition {condition.Priority}",
                    RequirementType = "OrganizationLocationMapping",
                    ResourceTypes = ["Location"],
                    SourceFhirPath = condition.FhirPath.Trim()
                });
            }
        }

        return plan;
    }

    /// <summary>
    /// Resolves the normalization suite and creates the appropriate operations and sequences
    /// via the Normalization API for the given facility. Replaces the legacy
    /// <c>FacilitySetupHelper.EnsureNormalizationConfigAsync</c> which only created a single
    /// hard-coded CopyProperty operation.
    /// </summary>
    private async Task<NormalizationSuiteResolution> EnsureNormalizationFromSuiteAsync(
        INormalizationServiceClient normalizationClient,
        IAutomationOutput output,
        string facilityId,
        Guid? suiteId,
        CancellationToken cancellationToken,
        NormalizationSuiteResolution? preResolved = null)
    {
        static string[] GetPlannedResourceTypes(NormalizationOperationDefinition planned)
            => planned.ResourceTypes
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        static List<NormalizationOperationApiModel> BuildKeyedOperationPool(
            List<NormalizationOperationApiModel> operations,
            string name,
            string operationType)
            => operations
                .Where(op => string.Equals(op.Name ?? string.Empty, name, StringComparison.Ordinal)
                             && string.Equals(op.OperationType ?? string.Empty, operationType, StringComparison.Ordinal))
                .ToList();

        static bool TryTakeMatchingOperation(
            List<NormalizationOperationApiModel> candidates,
            string[] plannedResourceTypes,
            out NormalizationOperationApiModel? matched)
        {
            matched = candidates.FirstOrDefault(op =>
            {
                var returnedResourceTypes = op.OperationResourceTypes
                    .Select(ort => ort.Resource?.ResourceName)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return plannedResourceTypes.SequenceEqual(returnedResourceTypes, StringComparer.OrdinalIgnoreCase);
            }) ?? candidates.FirstOrDefault();

            if (matched == null)
                return false;

            candidates.Remove(matched);
            return true;
        }

        async Task<List<NormalizationOperationApiModel>> FetchAllFacilityOperationsAsync()
        {
            var all = new List<NormalizationOperationApiModel>();
            const int pageSize = 100;
            var pageNumber = 1;

            while (true)
            {
                var resp = await normalizationClient.SearchFacilityOperationsAsync(
                    facilityId,
                    pageSize: pageSize,
                    pageNumber: pageNumber,
                    cancellationToken: cancellationToken);

                if (!resp.IsSuccessStatusCode || resp.Body?.Records == null)
                    throw new InvalidOperationException($"Failed to search normalization operations for facility '{facilityId}' on page {pageNumber}. HTTP {(int)resp.StatusCode}");

                all.AddRange(resp.Body.Records);

                var totalPages = resp.Body.Metadata?.TotalPages ?? 1;
                if (pageNumber >= totalPages)
                    break;

                pageNumber++;
            }

            return all;
        }

        var resolution = preResolved ?? await _normalizationSuiteResolver.ResolveAsync(suiteId, cancellationToken);

        var existingOperations = await FetchAllFacilityOperationsAsync();
        if (existingOperations.Count > 0)
            output.WriteLine($"Normalization config for facility '{facilityId}' already has {existingOperations.Count} operation(s). Reconciling suite configuration idempotently.");

        output.WriteLine($"Using normalization suite: {resolution.SuiteName} ({resolution.Operations.Count} operation(s))");

        if (resolution.Operations.Count == 0)
        {
            output.WriteLine("Normalization suite has no operations — skipping normalization configuration.");
            return resolution;
        }

        // Track sequence intent in suite order; operation IDs are resolved after reconciliation.
        var createdOpsByResourceType = new Dictionary<string, List<(Guid OpId, int Order)>>(StringComparer.OrdinalIgnoreCase);

        var existingPoolByKey = existingOperations
            .GroupBy(op => (op.Name ?? string.Empty, op.OperationType ?? string.Empty), EqualityComparer<(string Name, string Type)>.Default)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var opDef in resolution.Operations)
        {
            var plannedResourceTypes = GetPlannedResourceTypes(opDef);
            var key = (opDef.Name ?? string.Empty, opDef.OperationType ?? string.Empty);
            if (existingPoolByKey.TryGetValue(key, out var existingCandidates)
                && TryTakeMatchingOperation(existingCandidates, plannedResourceTypes, out var existingMatch)
                && existingMatch != null)
            {
                output.WriteLine($"  Using existing operation: {opDef.Name} ({opDef.OperationType}) for [{string.Join(", ", plannedResourceTypes)}]");
                continue;
            }

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
                case "CopyLocationAliasToTypeIteratively":
                    apiOp.MaxIterations = opDef.MaxIterations;
                    apiOp.SplitOnComma = opDef.SplitOnComma;
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
                throw new InvalidOperationException($"Failed to create normalization operation '{opDef.Name}' ({opDef.OperationType}) for facility '{facilityId}'. HTTP {(int)createResp.StatusCode}");
            }

            output.WriteLine($"  Created operation: {opDef.Name} ({opDef.OperationType}) for [{string.Join(", ", opDef.ResourceTypes)}]");

        }

        // Create sequences per resource type.
        // We need to get operations back from the API since the create response may not give IDs directly.
        // Instead, re-search to find newly created ops and build sequences.
        var allReturnedOperations = await FetchAllFacilityOperationsAsync();

        if (allReturnedOperations.Count > 0)
        {
            var availableByKey = allReturnedOperations
                .GroupBy(op => (op.Name ?? string.Empty, op.OperationType ?? string.Empty),
                    EqualityComparer<(string Name, string Type)>.Default)
                .ToDictionary(g => g.Key, g => g.ToList());

            for (var i = 0; i < resolution.Operations.Count; i++)
            {
                var planned = resolution.Operations[i];
                var key = (planned.Name ?? string.Empty, planned.OperationType ?? string.Empty);
                if (!availableByKey.TryGetValue(key, out var candidates) || candidates.Count == 0)
                {
                    throw new InvalidOperationException($"Could not map normalization operation '{planned.Name}' ({planned.OperationType}) from API search results for facility '{facilityId}'.");
                }

                var plannedResourceTypes = GetPlannedResourceTypes(planned);
                if (!TryTakeMatchingOperation(candidates, plannedResourceTypes, out var matched) || matched == null)
                    throw new InvalidOperationException($"Could not map normalization operation '{planned.Name}' ({planned.OperationType}) from API search results for facility '{facilityId}'.");

                foreach (var resourceType in plannedResourceTypes)
                {
                    if (!createdOpsByResourceType.TryGetValue(resourceType, out var mapped))
                    {
                        mapped = [];
                        createdOpsByResourceType[resourceType] = mapped;
                    }

                    mapped.Add((matched.Id, i + 1));
                }
            }

            foreach (var (resourceType, ops) in createdOpsByResourceType)
            {
                var sequences = ops
                    .OrderBy(o => o.Order)
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
                    throw new InvalidOperationException($"Failed to create normalization sequence for resource type '{resourceType}' in facility '{facilityId}'. HTTP {(int)seqResp.StatusCode}");
            }
        }

        return resolution;
    }
}
