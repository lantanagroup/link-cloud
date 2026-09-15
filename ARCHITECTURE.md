# Link Cloud architecture

How the platform is put together, and the decisions that are not obvious from any single file. Referenced from `AGENTS.md`; read this before changing anything that crosses a service boundary.


### .NET service layout (Clean Architecture, repeated per service)

Most `DotNet/<Service>/` projects follow the same five-layer shape:

- `Presentation/Endpoints/` — minimal-API endpoints (`/api/<servicename>/...`), the only external surface.
- `Application/` — Commands/Queries, Validators (FluentValidation), Models, Interfaces, Listeners, Services. Business logic lives here. Two hand-rolled shapes are used (there is **no** MediatR — the only reference is an unused `PackageReference` in `DotNet/Submission/Submission.csproj`): most services use one interface + implementation per operation with an `Execute(...)` method (e.g. `ICreateUser`/`CreateUser` in Account/Notification/Admin.BFF), injected directly via DI; **DataAcquisition** instead uses per-aggregate `Manager` (writes) + `Queries` (reads) classes over an `IDatabase` unit-of-work.
- `Domain/Entities/` — persistence-agnostic entities.
- `Persistence/` — EF Core `DbContext` + `Repositories` (SQL Server) **or** Mongo repositories. The DB provider is selected at startup via the `DatabaseProvider` config key.
- `Infrastructure/` — DI extensions, telemetry, health checks, logging composition (`Program.cs` wires it all up).

Don't add cross-layer references that bypass this shape; the `Application` layer never references `Persistence` types directly — it goes through `Application/Interfaces/`.

### `DotNet/Shared` is the contract layer

Every .NET service references `DotNet/Shared/Shared.csproj`. It owns:

- Kafka primitives — `Application/Listeners/BaseListener.cs`, `Application/SerDes/`, `Application/Filters/`, retry/DLQ handling. Most consumers inherit from `BaseListener` rather than wiring `IConsumer<,>` by hand.
- Cross-service contracts — `Domain/Entities/`, integration event models in `Application/Models/`.
- Auth, middleware, telemetry, health, redaction wiring used by every service's `Program.cs`.
- Constants — `Settings/KafkaConstants.cs` (header names like `X-Correlation-Id`, `X-Retry-Count`, the `KafkaConnection` config section), `Settings/ConfigurationConstants.cs`.

When you add a new Kafka topic, add it to `topics.txt` (the create-topics scripts in `Scripts/` read from it) and use the shared header constants — don't redefine them per service.

### Kafka topic conventions

Every functional topic has an `-Error` and `-Retry` companion (see `topics.txt`). Producers/consumers should route through the shared retry/DLQ filters rather than implementing their own.

Per-resource work runs in tight in-process loops that execute **hundreds of thousands to millions of times per run** — the `FhirApiService` acquire loop, Normalization's operation-sequence loop, and MeasureEval's Mongo bulk-upsert. Log lines on these paths must be `Debug` level or the namespace must be filtered in Serilog config — `Information` on a hot path will flood Loki and is treated as a defect. Note: resource bodies no longer ride Kafka per-resource (see *Resource transport* below); the `ResourcesAcquired` / `ResourcesNormalized` topics now fire roughly **once per patient-correlation**, so the volume is in the loops over cache contents, not in topic traffic.

### Configuration registry

`/app-config.yaml` at the repo root is the curated catalog of config keys that must be provisioned per environment, split into `global` (shared) and per-service sections, plus a `serviceMeta` block giving each service its App Configuration label and runtime. **Any new required config key (no default) must be added there in the same PR.** CodeRabbit will block PRs that introduce required config without updating this file.

It is deliberately *not* exhaustive. `docs/config-key-inventory.md` is the complete list of every key the code reads, generated from source — add a key to the catalog only if it is provisioned per environment, has no safe default, or is a documented operational knob.

`required: true` means a row must exist in **all three** environment stores. An entry with a working shipped default should be `required: false` with that value recorded in `defaultValue`; requiring it would mean provisioning one identical value three times.

**The per-environment values are not in this repo.** The catalog is here; the exports it is checked against (`app-config.<env>.json`, one per store) live in the private **`lantanagroup/link-cac`** repository — this one is public and those files are the deployed environments' real configuration. Everything under `Scripts/AzureAppConfig/` still runs from this repo root and defaults to a sibling clone at `../link-cac/Config`; override with `LINK_CAC_CONFIG_DIR` or `--config-dir`. Adding a `required: true` key here means adding its rows there, in the same pair of PRs.

**CI here never reads `link-cac`** — no token, no checkout. This repo is public in both directions that matter: its Actions logs are world-readable (and `check_required_config.py` reports private store keys and labels, even though it prints no values), and a read token in its secrets is readable by anyone with write access via a workflow change on a same-repo PR. So `.github/workflows/appconfig-catalog-check.yml` runs only what needs nothing external: the unit tests and `validate_app_config_schema.py` (the schema check is also in the pre-commit hook).

**The required-key check runs in `link-cac`**, against a public checkout of this repo, on every PR there plus daily. A `required: true` key added here is therefore *not* blocked at merge — the daily run is what catches it, and the fix (adding the rows) belongs in `link-cac` anyway. Run `Scripts/AzureAppConfig/check_required_config.py` and `reconcile_config_catalog.py` locally against a sibling clone before merging a catalog change.

Common config flows: env vars → `appsettings.{Environment}.json` → `Program.cs` registration. Services read DB provider from `DatabaseProvider`, JWT authority from `Authentication:Schemas:LinkBearer:Authority`, Kafka brokers from `KafkaConnection:BootstrapServers`.

**Azure App Configuration outranks environment variables** in both runtimes — Java via `overrideSystemProperties=true`, .NET because `AddAzureAppConfiguration` is appended after the env-var source. Setting an env var on a pod is silently ignored for any key the store defines. Java keys are stored slash-separated (`/spring/datasource/url`) and catalogued dotted (`spring.datasource.url`); see `Config/README.md` in `link-cac`.

### Automation / E2E test architecture

Three projects compose into the test surface:

- `DotNet/Automation` — platform-agnostic FHIR generator + prediction model. No Link dependencies. `FhirGenerationPipeline` streams patient bundles to FHIR, builds a `GenerationManifest`, and runs `QueryPlanAcquisitionSimulator` + `CqlFilterSimulator` to predict exactly which resources will reach each downstream layer.
- `DotNet/Automation.Link` — Link-specific orchestration: facility setup, report-generation trigger, `BackgroundDiagnosticsMonitor` (Loki/Kafka/progress/milestone probes), and the validators (`ReportAbsManifestValidator`, `ReportDatabaseValidator`, `DataAcquisitionDatabaseValidator`, `NormalizationDatabaseValidator`, `TenantDatabaseValidator`, `ValidationResultsValidator`).
- `DotNet/Automation.UI` — Spring/MVC-style web UI (`Scenarios` page) that runs the same generators against a live stack. Backend E2E suites and UI scenarios share seeds, patient counts, resource ranges, and prefixes — both hosts produce **bit-for-bit identical** FHIR input, so a failing E2E test can be reproduced from the UI and vice versa.

The validators use **strict equality**, not `actual >= expected`. When debugging a failure, the error message names the patient, resource type, expected count, and actual count — that points at the exact boundary that drifted (generator, query plan, CQL filter, normalization op, or a write-through).

### Pipeline architecture (report generation)

The "Census → DataAcq → ..." summary at the top is the linear story. This expands on the structural choices that aren't obvious from any one file.

#### Two convergent entry points + per-(patient × measure) scaffold

A report run starts either ad-hoc or scheduled. Both flows ultimately produce a `DataAcquisitionRequested` event per patient and pre-allocate the same SQL scaffold in `link-report` (`ReportSchedule` → `ReportEntry` per patient → `EntryMeasureReport` per measure, starting at `EntryCreated`). Every subsequent stage *updates* these rows; no other service inserts into `link-report`. Each `MeasureReportGenerated` flips one `EntryMeasureReport`; when all of a patient's rows are terminal, `PatientAggregator.AggregateToABS` runs.

**Ad-hoc path** (UI / regenerate): `Tenant.FacilityController` → `GenerateReportRequested` → `Report.GenerateReportListener`. One listener does both jobs in one shot — builds the `ReportSchedule` + per-patient scaffold (cohort from `Census.GetPatientList`, manual list, or old entries on regenerate), then immediately fires `DataAcquisitionRequested` per patient.

**Scheduled path** is fragmented across four listeners and a Quartz timer; **the scaffold and the acquisition trigger are built separately, by different services, at different times.** `Tenant.Jobs.ReportScheduledJob` Quartz → `ReportScheduled`, consumed by *two* services:

- `Report.ReportScheduledListener` creates the `ReportScheduleModel` row (no patient scaffold yet) and schedules a Quartz `EndOfReportPeriodJob` to fire at `ReportEndDate`.
- `QueryDispatch.ReportScheduledEventListener` records the active reporting window in QD's own SQL (`ScheduledReportEntity`).

Per-patient work doesn't begin here. It begins when `Census` later emits `PatientEvent` (admit/discharge), which is consumed by **two complementary halves**:

- `Report.PatientEventListener` (Admit + Discharge): for each non-finalized `ReportScheduleModel` for the facility, ensures a `ReportEntryModel` + per-`ReportType` `EntryMeasureReportModel` exists for `(scheduleId, patientId)`. **Incrementally builds the same scaffold `GenerateReportListener` builds all-at-once.**
- `QueryDispatch.PatientEventListener` (Discharge only): persists a `PatientDispatchEntity` (delayed work-queue row) and schedules a Quartz trigger at `now + DispatchSchedule.Duration` (ISO-8601 timespan per facility, e.g. `PT2H`). When the trigger fires, `QueryDispatchJob.Execute` produces `DataAcquisitionRequested` (`QueryType=Initial`, `ReportableEvent=Discharge`) and deletes the dispatch row.

Neither half makes sense alone — Report builds the rows that the downstream pipeline updates; QueryDispatch fires the work that does the updating. Both flows converge at `DataAcquisitionRequested`; from there on the pipeline doesn't know which path triggered it.

Quartz storage is SQL-backed and clustered for every service that uses it (`UsePersistentStore` / `UseSqlServer` / `UseClustering` in shared `RegisterQuartzDatabase`), so triggers survive container restarts via the `quartz.QRTZ_*` tables. `QueryDispatch.ScheduleService.StartAsync` *also* walks the `patientDispatches` SQL table on boot and reconciles Quartz state to it (adds missing triggers, removes orphans) — belt-and-braces, not the only thing preventing dispatch loss.

#### Two-phase MeasureEval — the only backward arrow

`MeasureEval` (Java) gates the EHR's full fetch on whether a patient qualifies:

- **INITIAL** — minimal bundle + cheap CQL to test the measure's `Initial Population`. Not reportable → terminal `MeasureReportGenerated` with `payloadUri=null`. Reportable → emit a **new** `DataAcquisitionRequested` with `QueryType=SUPPLEMENTAL`; same DataAcq → Norm → MeasureEval path runs again with richer data.
- **SUPPLEMENTAL** — full CQL evaluation; `BlobStorageService.storePatientInBlobStorage` writes the patient bundle AND emits `MeasureReportGenerated` with the blob URI populated.

The regenerate path (`EvaluationRequested`) skips the supplemental loop — it assumes the original run's resources are still in MongoDB.

#### Resource transport — shared cache, not Kafka payloads (and the tail gate)

FHIR resource **bodies do not travel on Kafka**. They are written to a shared `IResourceCache` (Redis or Azure Blob/ABS — `RedisResourceCache` / `ABSResourceCache` / `HybridResourceCache` in `DotNet/Shared`) keyed by `correlationId:resourceType` via `UpdateCorrelationCache`. DataAcquisition's own SQL only records the acquired **IDs** (`DataAcquisitionLog.ResourceAcquiredIds`), never the bodies. The Kafka events that flow downstream carry **cache pointers**, not resources.

The "tail" is now an internal completion gate, not a marker message. A single `DataAcquisitionRequested` fans out into many sibling query logs (one per resource type / reference batch); `DataAcquisitionLogManager.TryCompleteTailAsync` fires **once per `(facility, correlationId, queryPhase)` group**, only after every sibling log is terminal (guarded by a distributed semaphore). `AcquisitionProcessorBackgroundService.TryProduceTailMessageAsync` produces that single `ResourcesAcquired` event inline; `TailMessageRecoveryJob` (Quartz) is the backstop if the inline path is missed.

From there the whole correlation is processed as one unit at each hop — there are **no per-resource Kafka messages and no `AcquisitionComplete`/`Resource=null` marker** on these topics:

- **`ResourcesAcquired`** (`ResourcesAcquiredValue`) carries `CacheType` + `CacheKeys` (the `correlationId:type` keys). `Normalization.ResourcesAcquiredListener` rehydrates each key from the cache, applies the facility's operation sequences per resource, writes the results back to the cache, and produces **one** `ResourcesNormalized`.
- **`ResourcesNormalized`** (`ResourcesNormalizedValue`) carries `CacheType` + `CacheKey = correlationId`. `MeasureEval`'s `AbstractResourceConsumer.process` (Java) reads **all** resources for that correlation from the cache in one shot, builds a single `Bundle`, runs **CQL once**, then bulk-upserts the resources to MongoDB (deterministic `_id = uuid(facilityId:correlationId:resourceType:resourceId)`). On an INITIAL pass that turns out reportable, the Mongo write and cache cleanup are deliberately skipped so the SUPPLEMENTAL pass can reuse the cached resources.

#### MongoDB usage

**`MeasureEval` and `Automation.UI` use MongoDB, in separate databases.** MeasureEval's collections: `Resource` (one document per FHIR resource; `_id` is a deterministic UUID derived from `facilityId:correlationId:resourceType:resourceId` — `patientId` is stored as a field but is **not** part of the document identity) and `PatientReportingEvaluationStatus` (one per (facility, correlationId), with an inner `Report` per evaluated measure). Documents are written by MeasureEval's bulk-upsert as it consumes `ResourcesNormalized` (see *Resource transport* above). **Regenerate depends on these documents still existing**; retention policy interacts with regenerate-capability.

Every other service is SQL Server. `Automation.UI` is not a pipeline service: it is the test-automation host, and its database is addressed separately — `MongoDB:ConnectionString` shared and unlabeled, `MongoDB:DatabaseName` under the `Link Automation UI` label — holding the API-health and automation-run collections (`api_health_runs`, `automation_runs`, `automation_query_plan_templates`, and others) plus its Data Protection keyring. It shares the server, not the database, and nothing in the pipeline reads those collections. `Notification` and `Shared` carry a `MongoDB.Driver` package reference but no Mongo code path: Notification's `DatabaseProvider` switch throws on anything but SQL Server.

#### Internal vs external blob containers — the trust boundary

Two separate Azure Blob containers (Azurite locally; different storage accounts in prod). `internal/` is working storage written by `MeasureEval` and `Report.PatientAggregator`, read by `Validation` and `Submission`. `external/` is submission output — **only `Submission.SubmitPayloadListener` writes here**, and the `internal → external` upload IS the submission act. **There is no HTTP POST to NHSN, no SFTP, no FHIR `$submit-data`** — anything downstream of `external/` lives outside this codebase. A file in `external/` ⟺ one `PayloadSubmitted` event was emitted for it.

#### Convergent completion (no central coordinator)

`Report.PayloadSubmittedListener` (per-patient branch) re-invokes `ReportManifestProducer.Produce` after each patient. `Produce` is gated on `EndOfReportPeriodJobHasRun AND AreAllEntriesCompleteAsync`; almost every call is a no-op until the **last** patient satisfies both gates → manifest → `SubmitPayload(ReportSchedule)` → terminal `ReportSchedule.Status = Submitted`. The same pattern appears one level lower in `MeasureReportGeneratedListener`: each MRG checks `entry.MeasureReports.All(... is terminal)` to decide whether to aggregate. No "are we done?" loop anywhere.

#### Two-flavor SubmitPayload + PayloadSubmitted

Both topics carry a `PayloadType` enum: `MeasureReportSubmissionEntry` (per patient, source = `patient-{id}.ndjson`) and `ReportSchedule` (per report, source = `manifest.ndjson`). `Submission` handles both uniformly. `Report.PayloadSubmittedListener` branches: per-patient flips `ReportEntry.SubmissionStatus`; per-report sets `ReportSchedule.Status = Submitted` (terminal).

#### Three listener styles coexist

- **.NET `BaseListener<T,K1,V1,K2,V2>` derived** (`DotNet/Shared/Application/BaseListener.cs`) — subclasses override `ExecuteListenerAsync`. Used by `DataAcquisition`.
- **.NET hand-rolled `BackgroundService`** with `ProcessMessageAsync` or inline lambda — used by all 6 `Report` listeners, both `QueryDispatch` listeners, `Normalization`, `Submission`, `Census`.
- **Java `AbstractResourceConsumer<T> extends AsyncListener`** (Spring Kafka) — used by `MeasureEval` and `Validation`.

All commit Kafka offsets manually (`EnableAutoCommit = false`); per-message handlers must be idempotent.

#### Kafka key strategy is intentional per topic

`facilityId` (string) for per-facility ordering on most request/response topics. `LogId` (long) on `ReadyToAcquire` for maximum worker spread — re-keyed at the `dataacq → dataacq-worker` boundary deliberately. Typed `ResourceKey` / `SubmitPayloadKey` etc. elsewhere. **`MeasureReportGenerated` has a `Null` key** → round-robin, creating a race in `Report.MeasureReportGeneratedListener.readyForAggregation` when scaling Report horizontally (same-patient MRGs land on different consumer instances → duplicate aggregation, duplicate `ReadyForValidation`). Not observed in single-instance dev.

#### Known latent issues

- **`MeasureReportGenerated` null-key race** (above) — duplicate work under multi-instance Report.
- **`Validation.ReadyForValidationConsumer` valid/metrics inconsistency** — `valid` Kafka field is `true` for a patient whose only issues are uncategorized (`allMatch` on empty returns true), but the metrics block counts uncategorized as `Failed`. Event and dashboard disagree on the same patient.
