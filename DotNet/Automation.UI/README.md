# Automation.UI

`Automation.UI` is the interactive web host for Link automation runs. It lets users create and
manage test scenarios, configure FHIR query plans, start runs, watch progress in real time,
inspect pipeline state, and review validation outcomes -- all from a browser.

It composes:

- `Automation` (generation + prediction modeling)
- `Automation.Link` (Link-specific orchestration, diagnostics, and validators)
- `LinkSdk` (service API clients)

This README is aimed at three audiences:

- **Product owners / project managers** -- sections 1-4 describe what the UI does and how
  users interact with it.
- **QA** -- sections 5-7 and 15 describe run workflows and API Health execution/coverage.
- **Developers** -- sections 8-15 describe the code structure, persistence model, and
  extension points.

---

## 1. What this project is

At a high level, `Automation.UI` is a control plane and observability surface for end-to-end
pipeline tests. It provides:

1. **Scenario management** -- create, edit, clone, and delete reusable test configurations.
2. **Query plan management** -- define which FHIR resource types are acquired during data
   acquisition.
3. **Organization Resource Map management** -- define reusable org-location matching templates
   (FHIRPath conditions) used to scope reporting to organization-relevant encounters/resources.
4. **Normalization suite management** -- define reusable normalization operations,
   sequences, and suites; select suites per scenario.
5. **Scenario execution** -- start, cancel, and delete runs.
6. **Live telemetry** -- SignalR-driven logs, status, and dashboard updates.
7. **Dashboard analytics** -- KPIs, success rates, 14-day run histograms.
8. **Persistent run history** -- MongoDB-backed state across restarts.
9. **Snapshot-based inspection** -- pipeline domains pre-aggregated for UI rendering.
10. **Validation-backed confidence** -- report, ABS, DA, normalization, tenant, validation
   checks.
11. **API Health automated testing** -- service-oriented endpoint/status-path verification with
   seeded test data, streaming progress, and run history.

---

## 2. Architecture

```
Automation.UI (ASP.NET Core MVC + Razor views + SignalR)
+-- Controllers/
|   +-- RunsController          dashboard, run lifecycle, snapshot/log APIs
|   +-- ScenariosController     inline CRUD for scenario templates
|   +-- QueryPlansController    inline CRUD for query plan templates
|   +-- OrganizationResourceMapsController inline CRUD for org resource map templates
|   +-- NormalizationsController inline CRUD for normalization operations/sequences/suites
|   +-- ApiHealthController     API Health dashboard + SSE run streaming
|   +-- Api/AutomationRunsApiController  external start/status API
|   +-- Api/ApiHealthRunsApiController  external API Health start/status API
+-- Views/
|   +-- Runs/Index.cshtml       dashboard: KPIs, run history, quick-launch
|   +-- Runs/Details.cshtml     per-run live status, logs, pipeline snapshots, DA logs
|   +-- Runs/Manifest.cshtml    Generation-Manifest deep dive (generated / predicted / actual)
|   +-- Scenarios/Index.cshtml  scenario list (uses shared editor modal)
|   +-- QueryPlans/Index.cshtml query plan list + inline modal editor
|   +-- OrganizationResourceMaps/Index.cshtml organization resource map template management
|   +-- Normalizations/Index.cshtml normalization operations/sequences/suites management
|   +-- ApiHealth/Index.cshtml  API test matrix, run controls, SSE/live updates
|   +-- Shared/_ScenarioEditorModal.cshtml   reusable scenario editor (markup + JS)
+-- Models/
|   +-- RunDashboardViewModel, RunDashboardStats, TestScenarioDefinition, --
+-- Services/
|   +-- AutomationRunManager    run lifecycle orchestration
|   +-- ApiEndpointRegistry     API Health endpoint catalog
|   +-- ApiHealthTestExecutor   per-endpoint execution abstraction
|   +-- ApiHealthExecutionRunManager  long-running API Health service/all runs
|   +-- ApiHealthSeedOrchestrator     seed run lifecycle + context propagation
|   +-- RunSnapshotOrchestrator background pollers per active run
|   +-- StoreBackedServicePoller polls pipeline domains and writes snapshots
|   +-- RunHub                  SignalR hub for run logs + dashboard updates
|   +-- RunExportService        builds diagnostics ZIP from persisted run artifacts
|   +-- ScenarioSeedService     seeds system scenarios at startup
|   +-- NormalizationSuiteSeedService seeds system normalization operations/sequences/suites
|   +-- NormalizationSuiteResolver resolves selected suite at run time
|   +-- ScenarioRunStartupRecoveryService cancel/delete recovery for interrupted scenario runs
|   +-- ApiHealthStartupRecoveryService reconciliation for interrupted API Health runs
+-- Services/Persistence/
    +-- MongoSnapshotStore      run summaries, logs, per-domain snapshots
    +-- MongoScenarioStore      saved scenario templates
    +-- MongoQueryPlanTemplateStore
    +-- MongoIndexManager       Cosmos/Mongo-compatible index initialization
```

The design intentionally separates:

- **Execution path** (`AutomationRunManager`) from
- **Read path** (`ISnapshotStore` + poller snapshots),

so UI reads are fast and never call backend services synchronously.

---

## 3. Controllers and user-facing workflows

### 3.1 `RunsController`

Primary execution surface. Provides the dashboard, run start/cancel/delete, and inspection
APIs.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Dashboard page (KPIs, active runs, recent history, quick-launch). |
| `RecentRunsPartial` | GET | Returns only the recent-runs table markup for in-place AJAX refresh/sort/paging. |
| `DashboardStats` | GET | Returns `RunDashboardStats` JSON for AJAX refresh. |
| `Start` | POST | Start a new automation run from a scenario configuration. |
| `Details` | GET | Per-run detail page with live logs and pipeline state. |
| `Manifest` | GET | Generation-Manifest deep-dive page (generated/predicted/actual analysis). |
| `ManifestData` | GET | JSON snapshot of the generation manifest for UI rendering. |
| `AbsUploadData` | GET | JSON ABS upload snapshot for the Generated-vs-ABS comparison. |
| `Status` | GET | JSON run status for polling. |
| `Export` | GET | Download `TestRunDiagnostics-{runId}.zip` for a completed run (`Succeeded`/`Failed`/`Cancelled`). |
| `Cancel` / `CancelJson` | POST | Cancel a running automation run. |
| `Delete` / `DeleteJson` | POST | Delete a run and its artifacts. |
| `PipelineSnapshot` | GET | Full diagnostic pipeline snapshot for a run. |
| `DataAcquisitionLogs` | GET | Paginated, sortable, **free-text searchable** DA query logs for a run. The optional `searchTerm` query parameter is trimmed and forwarded to the DataAcquisition service, which applies it as `PatientId LIKE %term%` (case-insensitive) OR exact `Id` match (when the term parses as a `long`) OR exact `ResourceType` match (when the term parses as a known FHIR ResourceType). The Run Details page debounces the search input and re-pages from the server on every change, so pagination behaves identically to the unfiltered case (page 1 of the filtered set, no client-side row hiding, no bulk pre-fetch). |
| `DataAcquisitionLogDetail` | GET | Individual DA log entry detail. |

The dashboard's **quick-launch** dropdown is populated from saved scenarios. Selecting a
scenario mirrors that scenario's saved values into the quick-launch form so the run
honours them: `ReportMethod`, `Seed`, `PatientCount`, `ResourcesPerPatientMax`,
`QueryPlanTemplateId`, `ReportPeriodStart` / `ReportPeriodEnd`, `CleanupFhirData`, and
`CleanupServiceData`. Without this mirroring, the static defaults
(`CleanupFhirData=true`, `CleanupServiceData=false`, no period) would silently override
whatever the user saved on the scenario.

Clicking the **+ New Scenario** button on the Runs page opens the shared scenario editor
modal inline -- a saved scenario is automatically appended to the dropdown and selected,
so the user can run the newly authored scenario without navigating away.

### 3.2 `ScenariosController`

Manages saved test scenario templates. All editing is inline via JSON endpoints -- there are
no separate Create/Edit/Details pages.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Scenario list page (uses shared editor modal). |
| `GetJson` | GET | Return a single scenario as JSON. |
| `SaveInline` | POST | Create or update a scenario (JSON body). Validates report period bounds and imported-patient inputs (see below). |
| `UploadImportedBundle` | POST | Upload/validate bundle JSON (multipart form), stage it in `automation_imported_bundles`, and return `bundleId` metadata for later save/classify calls. |
| `CloneInline` | POST | Clone an existing scenario (deep-copies cohorts and imported patient lists; resets `IsSystemScenario` to `false`). |
| `DeleteInline` | POST | Delete a non-system scenario. |
| `ClassifyImported` | POST | Classify an imported patient (by ID or supplied bundle JSON) and return per-measure Q/NQ eligibility, detected clinical scenario, and encounter date range. Drives the editor's auto-detect and Report-Period auto-suggest UX. |

System scenarios (seeded at startup by `ScenarioSeedService`) cannot be modified or deleted,
only cloned. Attempting to modify a system scenario returns `403 Forbidden`.

`SaveInline` also performs server-side validation that the editor cannot fully enforce on
the client side:

- Report Period end must be on or after Report Period start (when both are set).
- Each ID-based imported patient must have a non-empty `PatientId`. `BundleJson` is
  cleared so it is never persisted on ID rows.
- Each bundle-based imported patient must contain parseable FHIR with at least one
  `Patient` whose `id` matches the configured `PatientId` (if a `PatientId` was supplied;
  otherwise the bundle's Patient id is used). `Source` is forced to `Bundle` to keep the
  document self-consistent.
- Imported encounter dates are NOT required to sit inside the configured Report Period.
  A scenario with a date mismatch is a legitimate test case (proper disqualification by
  measure-eval); the editor surfaces the mismatch as a warning instead of blocking save.
- Uploaded bundle files are capped at 12 MB per file. Upload is content-addressed by hash,
  so identical JSON payloads reuse the same staged bundle record.

### 3.3 `QueryPlansController`

Manages FHIR query plan templates that define which resource types are acquired during data
acquisition. Follows the same inline editing pattern as Scenarios.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Query plan list page with inline modal editor. |
| `GetJson` | GET | Return a single query plan as JSON. |
| `GetDefaults` | GET | Return a new query plan pre-populated with defaults. |
| `AcquiredTypes` | GET | Return the list of acquired resource types for a plan. |
| `SaveInline` | POST | Create or update a query plan (JSON body). |
| `CloneInline` | POST | Clone an existing query plan. |
| `DeleteInline` | POST | Delete a non-system query plan. |
| `SetDefaultInline` | POST | Mark a query plan as the system default. |

System query plans cannot be modified or deleted. The default query plan is used for new
scenarios unless overridden.

### 3.4 `NormalizationsController`

Manages reusable normalization definitions used by scenarios:

- **Operations** (`NormalizationOperationDefinition`) -- unit operations (for example
  `RemoveExtensions`, `CopyProperty`, `CodeMap`, `ConditionalTransform`, `CopyLocation`).
- **Sequences** (`NormalizationSequenceDefinition`) -- ordered operation references.
- **Suites** (`NormalizationSuiteDefinition`) -- bundles of sequences and/or standalone
  operations, with optional default marker.

All edits are inline JSON POSTs with antiforgery and system-item protection semantics
(system items can be cloned, not modified/deleted).

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Normalization management page (operations / sequences / suites tabs). |
| `GetOperationJson` / `GetSequenceJson` / `GetSuiteJson` | GET | Fetch one definition by id. |
| `SaveOperation` / `SaveSequence` / `SaveSuite` | POST | Create or update non-system definitions. |
| `CloneOperation` / `CloneSequence` / `CloneSuite` | POST | Deep copy selected definition. |
| `DeleteOperation` / `DeleteSequence` / `DeleteSuite` | POST | Delete non-system definitions. |
| `SetDefaultSuite` | POST | Mark one suite as default. |

### 3.5 `OrganizationResourceMapsController`

Manages reusable Organization Resource Map templates used to configure Data Acquisition
org-location matching conditions per run.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Template list/editor page. |
| `GetJson` | GET | Return a single org resource map template as JSON. |
| `SaveInline` | POST | Create or update a custom org resource map template. |
| `CloneInline` | POST | Clone an existing template. |
| `DeleteInline` | POST | Delete a non-system template. |
| `SetDefaultInline` | POST | Mark a template as the default for new scenarios. |

Templates store one or more FHIRPath conditions (evaluated against `Location`) and are
applied with "any match" semantics.

### 3.6 `ApiHealthController`

Primary API Health UI surface. Provides dashboard rendering, run launch, SSE streaming,
active-run resume, and history APIs.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | API Health matrix page (grouped by service, latest status per endpoint). |
| `RunServiceStream` | GET | Start service-scoped API Health run; stream events via SSE. |
| `RunAllStream` | GET | Start run-all API Health run; stream events via SSE. |
| `RunStream` | GET | Attach to an existing API Health run id and stream events. |
| `ActiveRun` | GET | Returns currently active API Health run metadata for resume-on-refresh UX. |
| `History` | GET | Paged endpoint history from `api_health_runs` persistence. |

Execution events are streamed as `text/event-stream` (`phase`, `result`, `done`) and the
client updates row status/badges in real time.

### 3.7 `Api/AutomationRunsApiController`

Bearer-protected service-to-service API for scenario automation:

| Action | Method | Purpose |
|---|---|---|
| `POST /api/runs/start` | POST | Start a saved scenario by id and return `202 Accepted` with `runId`. |
| `GET /api/runs/{runId}/status` | GET | Poll lightweight run state (`Status`, `IsTerminal`, timestamps, error). |

### 3.8 `Api/ApiHealthRunsApiController`

API surface for external API Health orchestration (UI + pipeline flows):

| Action | Method | Purpose |
|---|---|---|
| `POST /api/api-health-runs/start-all` | POST | UI entry point with antiforgery token. Starts an "all services" API Health run. |
| `POST /api/api-health-runs/start-all-for-pipeline` | POST | Bearer-protected pipeline entry point for "all services" runs. |
| `GET /api/api-health-runs/{runId}/status` | GET | Poll aggregate endpoint counts, terminal state, and failed endpoint summaries. |
| `GET /api/api-health-runs/{runId}/results` | GET | Get all endpoint-level results emitted for the run. |

---

## 4. View architecture

All management views follow a **single-page inline editing** pattern:

1. **List table** -- shows all items with summary columns.
2. **Modal editor** -- opens over the list for create/edit/view.
3. **IIFE script block** -- self-contained JavaScript at the bottom of the page.
4. **JSON AJAX** -- all mutations use `fetch()` POST with `Content-Type: application/json` and
   the antiforgery token header.

URLs for AJAX endpoints are generated server-side via `@Url.Action("Action", "Controller")`
and embedded as JavaScript variables.

### Shared scenario editor modal

`Views/Shared/_ScenarioEditorModal.cshtml` is a reusable partial that contains both the modal
markup and its self-contained JavaScript IIFE. Any page can embed it with:

```razor
@await Html.PartialAsync("_ScenarioEditorModal")
```

The partial exposes a small public JavaScript API:

```js
// Open the editor for a new scenario
window.openScenarioEditor(null, 'edit');

// Open an existing scenario for edit or read-only view
window.openScenarioEditor(scenarioId, 'edit' | 'view');
```

And dispatches a DOM event on `document` after any successful server action:

```js
document.addEventListener('scenario-editor:changed', function (e) {
    // e.detail === { action: 'saved' | 'cloned' | 'deleted', id, name }
});
```

The Scenarios page reloads on this event; the Runs page appends the saved scenario to the
quick-launch dropdown, selects it, and fires the dropdown's `change` event so the Run button
becomes ready immediately.

### API Health view model

`Views/ApiHealth/Index.cshtml` is a service-grouped test matrix (not modal CRUD). It provides:

- **Run All** and **Run Service** controls.
- Real-time phase/result rendering through SSE.
- Seed-run status card (run link, live status, elapsed timer).
- Endpoint-level history modal backed by persisted API Health results.
- Run-start reset behavior (service-only or all rows) so each execution starts from pending state.

### Antiforgery

Browser-initiated POST endpoints use `[ValidateAntiForgeryToken]`. The token is rendered via
`@Html.AntiForgeryToken()` inside the modal and sent as a `RequestVerificationToken` request
header.

Exception: bearer-authenticated service-to-service endpoints under `Controllers/Api/*`
(`ApiBearerPolicy`) do not use antiforgery.

Antiforgery tokens are protected by ASP.NET Core Data Protection. `Automation.UI` persists the
Data Protection key ring to MongoDB so redeploying the UI does not invalidate current browser
tokens solely because the container restarted. Keys are stored in the configured Mongo database in
`DataProtection:KeyCollectionName` (default: `automation_data_protection_keys`).

By default, the Data Protection application name is environment-scoped:

```text
Link.Automation.UI:{ASPNETCORE_ENVIRONMENT}
```

This allows environments such as Dev and Test to use the same Mongo collection without sharing a
decryptable antiforgery/cookie payload boundary. Override `DataProtection:ApplicationName` only if
the value needs to be pinned to a deployment-specific name; it must remain stable across redeploys
and across all replicas of the same environment.

---

## 5. Scenario configuration model

A `TestScenarioDefinition` captures everything needed to run a test:

| Field | Purpose |
|---|---|
| `Name` / `Description` | User-facing identification. |
| `IsSystemScenario` | If `true`, seeded by the system and immutable. |
| `ReportMethod` | `Adhoc`, `ScheduledReport`, or `RegenerateReport`. |
| `SelectedMeasures` | Which profiled measures to test. |
| `Seed` | Deterministic generation seed. |
| `PatientCount` | Computed from cohorts plus imported patients (read-only on the editor). |
| `ResourcesPerPatientMin/Max` | Resource count range per patient. |
| `PatientCohorts` | List of cohort definitions (count, `CohortQualification`, per-measure eligibility, clinical profiles, resource range, inpatient pattern). |
| `NhsnOrganizationId` | NHSN reporting organization id applied to generated organization/facility context. |
| `QueryPlanTemplateId` | Optional override for the FHIR query plan (null = system default). |
| `OrganizationResourceMapTemplateId` | Optional override for org-location matching template (null = default template). |
| `NormalizationSuiteId` | Optional override for the normalization suite (null = system default suite). |
| `CleanupServiceData` | Remove facility config and run artifacts after completion. |
| `CleanupFhirData` | Expunge FHIR server data after completion. |
| `ReportPeriodStart` / `ReportPeriodEnd` | Optional reporting period (UTC). When null, the system defaults (`2023-01-01T00:00:00Z` / `2023-12-31T23:59:59Z`) are used. The runtime uses these to bound generated encounter windows and to drive the report's clinical period; the editor auto-suggests a value that encompasses any imported-patient encounter dates. |
| `ImportedPatientIds` | Patients fetched from the FHIR server by ID at run time. Their resources are not uploaded and not expunged on cleanup. |
| `ImportedPatientBundles` | Patients supplied as FHIR transaction bundles. The bundle is uploaded to the FHIR server during the run and expunged on cleanup like any generated patient. |

### Patient ID generation

There is no caller-configurable patient ID prefix. The underlying generator scopes every
generated resource ID to a per-run `RunTag` (a short hex GUID), which guarantees that
concurrent runs against the same FHIR server cannot collide. Generated patient IDs follow
`Patient-{RunTag}-{ordinal:D3}`; shared infrastructure follows
`{RunTag}-Loc-Hospital`, `{RunTag}-Org-Hospital`, etc. This applies to the system
scenarios, the Custom scenario kind, and any saved scenario.

### Imported patients

Imported patients are fully integrated into the run and into the Generation Manifest, so
prediction-vs-actual reconciliation works for them too. They come from one of two
sources:

| Source | Where the data comes from | Uploaded during the run? | Expunged on cleanup? |
|---|---|---|---|
| `ExistingId` | Pre-fetched via `Patient/{id}/$everything` (paginated) | No -- the data is assumed to already exist on the server. | No -- recorded in `GenerationManifest.PreExistingPatientIds` so cleanup skips them. |
| `Bundle` | Caller-supplied transaction bundle JSON | Yes -- treated like any uploaded patient. | Yes -- standard cleanup applies. |

The editor lets the user mix imported patients with cohort-generated patients freely. For
each imported patient the editor calls `ScenariosController.ClassifyImported` to:

- Run `ImportedPatientClassifier` against the patient's resources and seed the per-measure
  Q/NQ checkboxes (the user can override; their choice always wins on save).
- Best-effort match the patient's primary diagnosis to one of the 16 clinical scenarios
  for an informational scenario badge.
- Sniff the encounter `Period` values so the editor can auto-suggest a Report Period
  that encloses them (only fills empty fields; never overwrites user-set dates).

When an imported encounter falls outside the configured Report Period, the editor shows
a warning so the user knows to expect non-qualification. The save proceeds either way.

Bundle uploads are staged before save via `ScenariosController.UploadImportedBundle`
(multipart form upload). The scenario then persists only `UploadedBundleId` references;
bundle payloads are stored in `automation_imported_bundles` and deduplicated by content hash.

> **Important for Custom / Uploaded patients:** imported data is used as-authored. To get
> expected org-scoped acquisition and ABS outcomes, the selected Organization Resource Map
> must be compatible with the actual `Location` / `Encounter.location` shape in that data
> (for example matching the identifier systems/codes present on referenced locations).
> A broad/default map that fits generated synthetic data may not fit uploaded bundles.

### System scenarios

`ScenarioSeedService` seeds a set of canonical system scenarios at startup. Each one mirrors a
corresponding backend E2E test (same seed, patient count, resource range), so running the
UI scenario and the backend test produces equivalent FHIR input:

| System scenario | Seed | Patients | Resources |
|---|---:|---:|---:|
| Adhoc Report Test | 20260326 | 1 | 1000 |
| Multi Patient Test | 20260328 | 150 | 25-50 |
| Mega Patient Test | 20260327 | 1 | 5000 |
| Mega Multi Patient Test | 20260330 | 150 | 5000 / 25-50 |
| Scheduled Report Test | 20260326 | 1 | 1000 |
| Regenerate Report Test | 20260401 | 1 | 100 |
| Multi Measure Test | 20260420 | 2 | 250 |

These scenarios match `Tests/BackendE2ETests` entry by entry (see that project's README).

### Normalization suites and generation requirements plans

`Automation.UI` stores and manages normalization suites as Link-facing configuration, but it
does **not** hardcode suite behavior into the core generator. At run time:

1. The selected suite id (`NormalizationSuiteId`) is resolved to concrete operations via
   `NormalizationSuiteResolver`.
2. `RunExecutor` maps that resolved suite into a platform-agnostic
   `GenerationRequirementsPlan` (from the `Automation` project).
3. The generation plan is passed into both generation paths
   (`FhirGenerationPipeline` and `FhirBundleGenerator`) so generated data includes required
   trigger characteristics.
4. The original suite is still applied to Link Normalization service configuration for
   execution-time normalization.

This separation keeps `Automation` platform-agnostic while allowing `Automation.UI` to
author and enforce Link-specific normalization intent.

### Cohort model

Each cohort defines a group of patients sharing:

- **Patient count** -- how many patients to generate.
- **Measure eligibility** -- per-measure qualifying / non-qualifying checkboxes.
- **Clinical profiles** -- which of the 16 clinical scenarios to draw from (filtered by
  eligibility).
- **Resource range** -- min/max resources per patient.

The editor enforces measure eligibility constraints in the UI:

- ACH Monthly and Daily measures always qualify for every inpatient encounter, so the
  qualifying checkbox is always checked (non-qualifying ACH is not currently meaningful at
  the generation layer).
- The Hypoglycemic measure requires ACH Monthly to also be qualifying (dependency
  enforcement).

### Inpatient Pattern (UI-driven encounter timing)

Each cohort includes a `ScheduledInpatientPattern` that defines encounter timing relative to
the report period. This is authored in the shared scenario editor modal and persisted with
the cohort JSON.

Supported values:

- `AdmittedBeforePeriodRemainsInpatientAfterPeriod`
- `AdmittedBeforePeriodDischargedDuringPeriod`
- `AdmittedDuringPeriodRemainsInpatientAfterPeriod`
- `AdmittedDuringPeriodDischargedDuringPeriod`
- `AdmittedAndDischargedBeforePeriod`
- `AdmittedAndDischargedAfterPeriod`

The dropdown uses compact labels for readability and tooltip hints for the full wording.

#### Cohort Outcome + Pattern compatibility

The editor now treats cohort intent as explicit via `CohortQualification`:

- `Qualifying`
- `NonQualifying`

Pattern options are filtered by that choice:

- **Qualifying** cohorts can choose only patterns whose census behavior is
  `ExpectedInReport=true`.
- **Non-Qualifying** cohorts can choose only patterns whose census behavior is
  `ExpectedInReport=false`.

This prevents invalid UI combinations that previously caused prediction drift.

#### Measure checkbox rules by Cohort Outcome

The cohort measure-eligibility editor enforces hard constraints:

- **Qualifying cohort**
  - If one selected measure exists in the scenario, it cannot be unchecked.
  - If multiple measures exist, at least one must remain checked.
- **Non-Qualifying cohort**
  - No measure checkboxes can be selected.

These rules run on row add, measure toggle, cohort-outcome toggle, and global measure-list
changes.

#### Runtime impact

The UI host passes inpatient pattern + cohort qualification through run resolution so the
downstream generation/prediction model can align expectations with authored scenario intent:

- Pattern shapes encounter timing for both scheduled and non-scheduled runs.
- Scheduled/regenerate runs additionally use pattern-derived live census orchestration.
- Expected submitted/ABS sets are gated by measure eligibility **and**
  (`CohortQualification` + pattern inclusion semantics), reducing false validator failures.

#### Backward compatibility

Legacy scenarios (saved before these fields were first-class) are normalized on load/save:

- missing pattern defaults to
  `AdmittedBeforePeriodRemainsInpatientAfterPeriod`.
- missing `CohortQualification` is inferred from per-measure eligibility where possible.

This keeps existing saved scenarios runnable while adopting the new model.

---

## 6. Dashboard and real-time updates

### 6.1 Dashboard stats

`RunDashboardStats` provides aggregated KPIs computed from the last 14 days of run history:

- Total runs, succeeded, failed, cancelled, running, queued counts.
- Success rate percentage.
- Average duration for completed runs.
- 14-day histogram with per-day succeeded/failed/cancelled buckets.

### 6.2 SignalR hub

`RunHub` supports two subscription groups:

| Method | Group | Purpose |
|---|---|---|
| `SubscribeRun(runId)` | Per-run group | Receives `log` and `status` events for a specific run. |
| `SubscribeDashboard()` | `dashboard` group | Receives `dashboardUpdate` events when any run changes status. |

`AutomationRunManager` broadcasts to both groups on every status transition, enabling live log
streaming on the run details page and real-time dashboard KPI updates without polling.

### 6.3 API Health streaming model

API Health uses **SSE** (not SignalR) for long-running test execution streams:

- `ApiHealthExecutionRunManager` owns in-memory active run state and ordered event buffers.
- `ApiHealthController.StreamRunAsync` emits `phase`, `result`, and terminal `done` events.
- Browser-side code supports reconnect/resume by requesting `ActiveRun` then attaching with
  `RunStream?runId=...`.
- Seed orchestration is surfaced in-phase, including linked seed run metadata when available.

Why SSE here:

- API Health execution is a one-way server→browser event feed (`phase`/`result`/`done`) with no
  client-to-server hub messaging requirements.
- `EventSource` keeps the transport model small and explicit for this workflow (single long-lived
  HTTP response per stream).
- Existing run-log telemetry remains on SignalR (`RunHub`) where group subscription semantics are
  needed; API Health keeps a separate, purpose-fit stream model to avoid unnecessary hub coupling.

---

## 7. End-to-end run workflow

`AutomationRunManager.ExecuteAsync(...)` coordinates the full lifecycle:

1. **Resolve request into run options**
   - Scenario defaults + custom overrides.
   - Selected measures.
   - Patient cohorts/profiles expansion via `PatientCohortDefinition.ExpandProfiles()`.
   - Imported-patient lists merged in from the request and (when present) any saved
     scenario JSON via `ExtractImportedFromJson`.
   - Reporting period resolved via `ResolveReportPeriod` -- explicit request values win;
     otherwise the value falls back to the system default for the selected report method.
2. **Generate FHIR input**
   - `ImportedPatientLoader.LoadAllAsync` pre-fetches `Patient/{id}/$everything` and
     parses any supplied bundles, then `ComputeEncounterDateRange` widens the run's
     clinical period to enclose imported encounter dates so they are not silently
     disqualified by measure-eval.
   - `FhirGenerationPipeline.GenerateAndUploadAsync()` for streaming generation
     (generated cohort first, imported patients appended).
   - `GenerationManifest` is built incrementally during generation; ID-imported patients
     are recorded via `MarkPreExistingPatient` so cleanup skips them.
    - The resolved normalization suite is translated into a platform-agnostic
      `GenerationRequirementsPlan` and supplied to generation so produced resources include
      the required trigger characteristics.
   - When an Organization Resource Map template is selected, its conditions are applied to:
     - Data Acquisition org-location configuration for the run facility, and
     - generation/prediction modeling so expected ABS counts are org-scope aware.
   - `QueryPlanAcquisitionSimulator` runs per-patient with the resolved clinical period.
   - `CqlFilterSimulator` runs per-patient over the patient's qualifying measures only.
3. **Initialize validation dependencies** -- validation artifacts and categories.
4. **Load measure bundles** -- `MeasureLoader.LoadAllAsync()` (supports multi-measure).
5. **Ensure tenant/pipeline setup** -- facility, normalization config, query plans/config,
   query dispatch config.
6. **Generate report and monitor submission**
   - Start `BackgroundDiagnosticsMonitor`.
   - Poll until submission / critical failure / timeout.
7. **(Optional) regeneration path** -- regenerate report and switch pollers to the new report
   ID.
8. **Snapshot + artifact download + validation suite**
   - Full diagnostic snapshot.
   - External/internal ABS downloads.
   - Validators run via `RunValidator` wrapper with per-validator error isolation.
   - Partial `validatorResults` persisted after each validator completes.
9. **Cleanup** -- service-level and FHIR cleanup based on run config.

### Validation orchestration

Validators are wrapped in a `RunValidator` helper that:

- Catches exceptions per-validator so one failure doesn't prevent subsequent validators.
- Persists partial `validatorResults` snapshots after each step.
- Ensures partial results are visible in the dashboard even if a later validator throws.

Run status transitions are persisted and broadcast in real time via SignalR.

---

## 8. Predictive ABS/resource validation model

For profile-based runs the UI host builds a deterministic `GenerationManifest` and enriches it
with:

- Acquired resource types from the effective query plan.
- Parameter-query resource types.
- Simulated acquired keys per patient (`QueryPlanAcquisitionSimulator`).
- Organization Resource Map post-filtering over simulated acquired keys
  (`OrgResourceMapPredictionFilter`).
- CQL-referenced resource types (`CqlResourceTypeExtractor`).
- Per-resource CQL exclusions (`CqlFilterSimulator`, measure-family profiles, intersection
  semantics across the patient's qualifying measures).

That manifest is passed to `ReportAbsManifestValidator` and `ReportDatabaseValidator` for
strict prediction-vs-actual comparison. Generation does not predict `OperationOutcome`.
The ABS validator expects one only when Validation's
`pre-qualification.write-pre-qual-operation-outcome` flag is on **and** the patient
`FailedValidation`. Passing patients are not required to have one.

The CQL simulator is resource-aware, not just type-aware. In addition to checking that a
resource type is acquired and referenced by CQL, it applies known SDE `where` predicates per
resource. For example, `Specimen` prediction now models measure-specific behavior:

- ACH Monthly predicts only specimens whose `subject` is the evaluated patient and whose
  `collection.collected` overlaps an initial-population encounter.
- ACH Daily predicts only patient-owned specimens referenced by qualifying respiratory
  pathogen laboratory observations (COVID-19, influenza, RSV); it does not assume every
  acquired specimen appears in ABS.
- Hypoglycemic predicts only patient-owned specimens whose collection interval is fully
  during an initial-population encounter.

This matters on the `Runs/Manifest` page: a `Specimen` can be shown as generated and even
DataAcquisition-acquired through a reference query, but still be filtered from the predicted
ABS set when patient-context CQL retrieval or measure-specific SDE predicates do not include
it.

Likewise, when Organization Resource Map conditions are active, resources tied only to
non-org encounters are intentionally excluded from the predicted ABS set, even if they are
otherwise query-plan-acquired and CQL-referenced.

See `DotNet/Automation/README.md` section 8 for the full prediction formula and profile table.

### Generation Manifest page

`Runs/Manifest` provides a deep-dive UX for this model:

- **Overview tab** -- measure context, query-plan acquired types, CQL-referenced types,
  generated resource distribution (patient + shared).
- **Patients tab** -- per-patient generated counts and type badges.
- **Generated vs ABS tab** -- side-by-side prediction and actual ABS upload counts, per-type
  and per-patient deltas, pipeline explanation distinguishing type-level CQL reachability
  from resource-level SDE filtering, and explicit rows for pipeline-derived types (Patient,
  MeasureReport, OperationOutcome) whose counts are predicted deterministically.

The page is intended to answer "what did we generate?", "what did we predict?", and "what did
ABS actually contain?" with enough fidelity to debug drift quickly.

---

## 9. Core services

### `AutomationRunManager`

Primary orchestrator. Owns run state, cancellation workflow, per-run DI container creation,
validator invocation, and SignalR log/status broadcasts. Provides `GetDashboardStatsAsync()`
for the dashboard (queries the last 14 days of runs).

### `RunSnapshotOrchestrator`

Background service that reconciles active runs from persistence and ensures each has exactly
one poller:

- Starts pollers for newly active runs.
- Restarts pollers when run context changes (e.g., regenerated report ID).
- Performs a final poll on completion to flush terminal domain state.

### `StoreBackedServicePoller`

Per-run poller with one cadence for all key domains:

- `schedule`
- `entries`
- `populations`
- `acquisitionSummary`
- `measureResources`
- `validationResources`

Each domain write is independent and fault-isolated. Failures are logged to run logs without
taking down the whole poll cycle.

### `RunExportService`

Builds per-run diagnostics ZIP packages (`TestRunDiagnostics-{runId}.zip`) from persisted
data (run summary, logs, snapshots, manifest/ABS summaries, submission artifacts). Export is
allowed only for terminal runs to avoid racing in-flight pollers.

### Startup recovery hosted services

- `ScenarioRunStartupRecoveryService` reconciles non-terminal scenario runs on process start
  (cancel first, hard-delete fallback).
- `ApiHealthStartupRecoveryService` marks interrupted API Health execution runs as failed and
  reconciles any linked seed run.

### API Health services

- `ApiEndpointRegistry` discovers/normalizes endpoint definitions from all registered suites.
- `ApiHealthTestExecutor` executes endpoint/suite tests and persists results.
- `ApiHealthExecutionRunManager` coordinates service/all runs, emits ordered execution events,
  and handles run state transitions.
- `ApiHealthSeedOrchestrator` launches/monitors seed scenario runs, exposes seed context to
  suites, and performs teardown cleanup.
- `IApiHealthSeedContextAccessor` carries per-run seed payloads (facility/report ids and seed
  run metadata) across suite execution.

### `ISnapshotStore` + Mongo stores

Persistence abstraction used by both execution and UI reads:

- Run metadata and summaries.
- Live + persisted log lines.
- Per-domain snapshot payloads.
- `GetAllRunSummariesAsync(since)` supports date filtering to avoid unbounded collection
  scans.

This enables restart-safe run history and multi-instance read sharing.

---

## 10. Persistence model (MongoDB / Cosmos Mongo API)

Collections:

- `automation_runs` -- run summaries and status metadata.
- `automation_run_inputs` -- per-run input snapshot (`RunConfigurationJson`, scenario metadata,
  imported bundle ids) used for details-page hydration and restart-safe context.
- `automation_snapshots` -- per-run, per-domain snapshot payloads.
- `automation_logs` -- full run logs.
- `automation_scenarios` -- user and system scenario templates.
- `automation_imported_bundles` -- staged imported bundle payloads, metadata, and
  cross-scenario reference tracking.
- `automation_query_plan_templates` -- query plan templates.
- `automation_normalization_operations` -- normalization operation definitions.
- `automation_normalization_sequences` -- normalization sequence definitions.
- `automation_normalization_suites` -- normalization suite definitions.
- `api_health_runs` -- API Health endpoint execution history.
- `api_health_execution_runs` -- API Health run-level execution state (active/phase/timestamps)
  for resume/recovery.

### Document conventions

- All `Guid` primary keys use `[BsonId] [BsonRepresentation(BsonType.String)]`.
- All `Guid?` foreign keys (e.g., `QueryPlanTemplateId`) use
  `[BsonRepresentation(BsonType.String)]`.
- Complex nested objects (patient profiles, cohorts, query entries) are stored as serialized
  JSON strings to avoid BSON schema rigidity.

### `MongoIndexManager`

Initializes required indexes with Cosmos-safe behavior:

- Checks key-shape existence first.
- Avoids modifying incompatible existing unique indexes.
- Logs failures without blocking startup.

---

## 11. Authentication model

### Inbound (browser -- Automation.UI)

External authentication is handled at the infrastructure layer (domain-level OAuth2 via
reverse proxy or gateway). The app itself keeps default authorization pass-through, but it
also includes a fail-closed deployment guard:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});
```

When `Authentication:EnableAnonymousAccess=false` (default), a startup middleware returns
`503 Service Unavailable` for all non-`/health` requests. This prevents accidentally serving
the UI without an upstream authenticating proxy.

When `Authentication:EnableAnonymousAccess=true`, the UI serves requests normally (still
assuming upstream authentication controls in production).

### Inbound API bearer policy (service-to-service)

`ApiBearerPolicy` can be enabled for external API endpoints with:

- `Authentication:ApiBearer:Enabled=true`
- `Authentication:ApiBearer:Authority`
- `Authentication:ApiBearer:Audience`

When enabled, `api/runs/*` and `api/api-health-runs/start-all-for-pipeline` require a valid
JWT bearer token.

### Outbound (Automation.UI -- Link services)

- `Authentication:UseBearerForServiceCalls=true` (recommended) -- LinkSdk attaches Link bearer
  tokens via `ICreateSystemToken`.
- `Authentication:UseBearerForServiceCalls=false` -- outbound calls are anonymous.

Token generation settings come from `LinkTokenService` configuration (not user tokens).

---

## 12. Configuration

Primary configuration sources:

- `appsettings.json`
- `appsettings.Docker.json`
- Environment variables
- Optional Azure App Configuration + Key Vault references

Key sections:

| Section | Purpose |
|---|---|
| `Automation` | Runtime orchestration settings (FHIR, run behavior, and automation-specific integration settings). |
| `Loki` | Shared log-query settings consumed by automation diagnostics (`Url`, `App`). |
| `ServiceRegistry` | Base URLs for Link services used by `LinkSdk` clients. |
| `MongoDB` | Connection/database for UI persistence. |
| `Authentication` | Outbound bearer toggle, anonymous-access guard, and optional inbound API bearer settings. |
| `DataProtection` | Optional app name/key collection overrides for persisted key ring. |
| `LinkTokenService` | System-token signing/issuer settings for outbound service calls. |

---

## 13. Running

### Local

```bash
dotnet run --project DotNet/Automation.UI/Automation.UI.csproj
```

### Docker

```bash
docker compose up automation-ui
```

Port mapping: host `5256` -- container `5257`.

---

## 14. Notes

- Targets `.NET 8`.
- MVC + Razor views + SignalR (not Razor Pages `PageModel` routing, despite the workspace
  characteristics hint -- the project uses classic MVC controllers and `@Html.PartialAsync`).
- `RunHub.SubscribeRun(runId)` groups clients for per-run log streaming;
  `RunHub.SubscribeDashboard()` groups clients for dashboard-wide status updates.
- The read path is store-backed by design; backend service polling is centralized in
  background pollers.
- All management views use a single-page inline modal editing pattern -- no multi-page form
  workflows. The scenario editor is a single reusable partial shared between the Scenarios
  and Runs pages.
- System scenarios and system query plans are seeded at startup and cannot be modified, only
  cloned.
- System scenarios are kept in sync with `Tests/BackendE2ETests` so that UI and test
  executions are reproducible from either host.
- There is no caller-supplied patient ID prefix anywhere in the UI. Patient IDs are
  scoped per run by the underlying generator's `RunTag`. The legacy `PatientPrefix`
  hidden form field has been removed from the dashboard quick-launch form and from
  `StartScenarioRequest`.
- Imported patients (by ID and by uploaded bundle) are first-class scenario inputs.
  Bundles are validated server-side on save; ID-only patients are pre-fetched at run
  start so the run's reporting period can widen to enclose their encounter dates.
- Organization Resource Maps are first-class scenario inputs and directly affect both
  acquisition behavior and ABS prediction math. For uploaded/custom patients, choose a map
  that specifically matches the locations and encounter references present in the imported
  dataset.
- The Report Period (`ReportPeriodStart` / `ReportPeriodEnd`) is exposed in the editor
  and on the dashboard quick-launch row. When omitted, the system default for the chosen
  report method is used.
- Data-Acquisition log search is server-side. The Run Details DA-logs panel debounces
  the search input and re-issues a paged request with `searchTerm` on every change.

---

## 15. API Health automated testing workflow

API Health is a dedicated automation workflow for service API validation in `Automation.UI`.

### What it does

- Executes endpoint/status-path checks across registered service suites.
- Supports service-scoped and run-all execution.
- Persists endpoint history and latest status for dashboard hydration.
- Streams execution progress/results live to the browser.

### Seeded execution model

- Suites declare seed requirements via `GetSeedRequirements()`.
- When required, API Health launches `ApiHealthScenario` through `IAutomationRunManager`.
- The resulting seed payload (e.g., facility/report ids) is attached to execution context.
- Seed teardown runs after suite execution, preserving test reliability during run and
  performing cleanup at the appropriate lifecycle stage.

### Result model

Each endpoint result tracks:

- expected vs actual status code,
- pass/fail/skip semantics,
- duration,
- request metadata,
- response snippet/error context,
- execution timestamp.

Skipped steps are explicit (with reason) for paths that are intentionally non-deterministic or
cannot be self-seeded through API-only orchestration.
