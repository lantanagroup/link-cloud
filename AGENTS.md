## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

Link Cloud is an open-source, event-driven microservices platform for FHIR-based public health reporting. It ingests clinical data from EHRs, normalizes it, evaluates measures (CQL), validates, and submits results downstream. Services span three runtimes that all share one Kafka bus, one set of databases, and one local docker-compose stack:

- **.NET 8** services under `DotNet/` (most services) — ASP.NET minimal APIs, EF Core or MongoDB, Kafka via Confluent.Kafka.
- **Java 17 / Spring Boot 3** services under `Java/` (`measureeval`, `validation`, plus a `shared` lib and the HAPI FHIR test server).
- **Angular** Admin UI under `Web/Admin.UI/`.

The pipeline is roughly: `Census → DataAcquisition → Normalization → MeasureEval (Java) → Validation (Java) → Report → Submission`, glued together by Kafka topics in `topics.txt`. `QueryDispatch`, `Tenant`, `Account`, `Audit`, `Notification`, `Terminology`, `Admin.BFF` are supporting services.

## Common commands

### Local stack (required for E2E and most integration work)

```powershell
docker compose up -d                      # bring up the full stack from repo root
Scripts/check_health.sh link 60 10        # wait for services to become healthy (bash)
docker compose down -v --remove-orphans   # tear everything down (resets volumes)
```

Service ports are listed at the top of `docker-compose.yml` (e.g. fhir 6157, admin-bff 8063, kafka 9092, kafka-ui 9095, loki 3100, grafana 3000, azurite 10000, mssql 1433, mongo 17017). The root `.env` provides default credentials used by compose.

### .NET

```powershell
dotnet build link-cloud.sln                                                 # whole solution
dotnet build DotNet/Account/Account.csproj                                  # one service
dotnet test  DotNet/ServiceTests/ServiceTests.csproj                        # all .NET unit + integration tests
dotnet test  DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~Tenant   # one area
```

`ServiceTests` contains both unit tests (no infra) and integration tests (Testcontainers spins up SQL Server + Azurite — Docker must be running). xUnit collections keep integration tests serialized while unit tests run in parallel within the same invocation.

### Backend E2E (requires the docker-compose stack already up and healthy)

```powershell
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj                                          # all suites
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter FullyQualifiedName~AdhocReportTest
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter Category=ApiStabilityTest       # CI uses Category=
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --logger "console;verbosity=detailed"
```

Endpoints are read from env vars (see `Tests/BackendE2ETests/README.md` and `TestConfig.cs`); defaults match the local docker-compose ports. Each test seeds deterministic FHIR data and validates with **strict prediction-vs-actual reconciliation** — generated input drives an exact expected count for every downstream layer (manifest, ABS NDJSON, Report/DA/Normalization/Validation DBs), and a deviation in either direction fails the run.

### Java

```bash
cd Java
mvn clean test                                          # build + unit-test all modules (CI does this)
mvn -pl measureeval -am clean package                   # one module + its deps (the `shared` lib)
mvn -P cli -pl measureeval -am clean package            # build measureeval as a CLI jar (FileSystemInvocation main)
```

`Java/shared` is published to a private Maven repo; module `.m2/settings.xml` files read credentials from env vars (set by CI).

### Admin UI

```powershell
cd Web/Admin.UI
npm install
npm start                                               # ng serve on :4200, proxied via proxy.conf.json
npm test                                                # ng test (karma + jasmine)
npm run build
```

### EF Core migrations

Entity changes that persist via EF Core **must** ship a migration that supports both upgrade *and* downgrade. Migrations live alongside each service (e.g. `DotNet/Account/Migrations/`):

```powershell
dotnet ef migrations add <Name> --project DotNet/Account/Account.csproj
dotnet ef database update     --project DotNet/Account/Account.csproj
```

## Architecture

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

**Only `MeasureEval` uses MongoDB.** Collections: `Resource` (one document per FHIR resource; `_id` is a deterministic UUID derived from `facilityId:correlationId:resourceType:resourceId` — `patientId` is stored as a field but is **not** part of the document identity) and `PatientReportingEvaluationStatus` (one per (facility, correlationId), with an inner `Report` per evaluated measure). Documents are written by MeasureEval's bulk-upsert as it consumes `ResourcesNormalized` (see *Resource transport* above). Every other service is SQL Server. **Regenerate depends on these documents still existing**; retention policy interacts with regenerate-capability.

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

## General Guidance

* Logging messages in functionality that repeat excessively (such as consumption of ResourceNormalized events - which occurs for every resource the system process, millions of times) should be treated as DEBUG messages or the namespace/package should be ignored by default so that it does not produce excessive persisted log messages.
* The Scripts directory contains scripts used by developers and admins to help operate the system at runtime. These scripts must always abstract out sensitive variables into arguments. Arguments may default values to environment variables for ease-of use, when appropriate. Do not hard-code credentials or tenant identifiers. Name prefixes: `aca-*` Azure Container Apps, `docker-compose.*` local development, `k8s-*` Kubernetes.
* Changes to entities that are persisted with EntityFramework must always have a migration created for them, which ideally supports both upgarding *and* downgrading (in the event of failed system updates in other services).

### Application Configuration

The **catalog** — which keys exist and what they mean — is `/app-config.yaml` in this repository. The **values**, one export per environment, live in the private `lantanagroup/link-cac` repository under `Config/`. Everything in `Scripts/AzureAppConfig/` runs from this repository's root and looks for those exports at `../link-cac/Config`; override with `LINK_CAC_CONFIG_DIR`, or with `--config-dir` on the tools that accept it. `validate_aac_secrets.py` is the exception: it takes export paths positionally and has no `--config-dir`.

* Any new configuration key that is **required** (has no safe default) must be added to `/app-config.yaml` in the same PR. Prefer shipping a working default in `appsettings.json` / `application.yml` and recording it as `required: false` with a `defaultValue` — requiring a key means provisioning one identical value into every environment.
* **State `required` explicitly.** The schema declares `default: true`, but JSON Schema defaults are annotation only and are never applied during validation.
* A `sensitive: true` entry must **never** carry a `defaultValue`. Secrets are supplied per environment, never seeded from this file.
* A `required: true` key needs a row in **every** environment export in `link-cac`. **CI here does not verify that** — this repository's workflows never read `link-cac`, because its Actions logs are public. `link-cac` checks it on its own PRs and daily. Run `Scripts/AzureAppConfig/check_required_config.py` locally before merging a catalog change.
* **Azure App Configuration outranks environment variables** in both runtimes. Setting an env var on a pod is silently ignored for any key the store defines — change the store instead.
* **.NET reads colon-delimited keys** (`KafkaConnection:BootstrapServers:0`); **Java reads slash-prefixed keys** (`/spring/datasource/url`) which the Spring provider converts to dots. The catalog records Java keys in the dotted form while the store holds the slash form. A Java service never sees the colon rows.
* A row is identified by the pair **(key, label)**. Each service selects unlabeled rows first, then its own label. Adding a labeled row on top of an unlabeled one is safe; *moving* a key from unlabeled to labeled breaks every service that does not select that exact label. Labels are compiled into the services — the mapping is the `serviceMeta` block in `/app-config.yaml` — and a label containing `:` matches nothing at all.
* Secrets must be Key Vault references carrying `content_type: application/vnd.microsoft.appconfig.keyvaultref+json`. With any other content type the provider serves the literal text `{"uri": "..."}` as the value, so the service uses that string as its password. Never write a literal credential into an export.
* A row with `content_type: application/json` is flattened into child properties, so a key such as `authentication.authority` will not appear in an export — its parent `/authentication` blob will.

### REST Operations

The canonical specification lives on the Keystone site, which supersedes the old `link-ec` docs repository: https://keystone.lantanagroup.com/Entities/Details?type=design&id=api_guidance

- Ensure inputs are validated and sanitized before passing inputs onto business logic.
- Build APIs using **REST** principles over **HTTP**, designed around **resource-based models**.
- Ensure APIs are **platform independent**, **persistence agnostic**, and can **evolve independently from clients**.
- Implement **Swagger/OpenAPI documentation** for every API.
- Return standard **HTTP status codes** consistently (200, 201, 202, 204, 400, 401, 403, 404, 500, 502, 503).
- For server errors, return **500 with RFC Problem Details** payload including a `traceId`.
- **POST**
    - 201 + `Location` header when creating a resource (return safe representation only).
    - 200 or 204 for processing-only operations.
    - 400 for validation failures with error details.
- **PUT**
    - Update existing resources only (do not create).
    - Require matching `id` in URL and body; return 400 if invalid or mismatched.
    - Return 202 on successful update.
- **GET**
    - 200 with resource representation (exclude sensitive/internal fields).
    - 404 if not found.
    - Use UUIDs for sensitive identifiers to reduce enumeration risk.
- **Search/List endpoints**
    - Return 200 with a paged model (`records` + `metadata`).
    - Return 200 with an empty `records` array when nothing matches — a search that matches nothing
      is still a successful search. Do not return 204: it discards the `metadata` clients need to
      render paging, and forces every caller to special-case an absent body (Angular's `HttpClient`
      surfaces it as a `null` body, silently violating the declared response type).
    - Reserve 404 for a single resource fetched by id that does not exist.
    - Older endpoints predating this rule may still return 204 on empty; bring them in line when you
      are already changing them, not as a drive-by.
- **DELETE**
    - Return 204 on successful removal.
- Prefer **typed HttpClient clients with HttpClientFactory**; enable **header propagation** (e.g., `Authorization`).
- Follow route conventions:
    - Remove component names from internal routes.
    - Use **plural nouns**, **lowercase**, and **hyphen-separated** 

#### Points the list above leaves implicit

- **PUT is update-only, not upsert.** If the resource does not exist, return a Problem result; do not create it. An `id` that is not the expected UUID format is a `400` with `"Invalid Id format"`.
- **Search/list `metadata`** is `{ pageSize, pageNumber, totalCount, totalPages }`.
- **`500` is a fallback, not a thing to throw.** Use the precise code where one fits: `400` (client input failed validation), `401` (not authenticated), `403` (authenticated but not allowed), `404` (not found), `502`/`503` (upstream or availability).
- **Why UUIDs for sensitive identifiers:** incrementing or guessable ids leak existence and enable enumeration, because a `403` where a `404` would otherwise appear tells an attacker the id is real.
- **URL versioning is reserved, not in use.** Do not add `/v1/` segments unless the specification adopts them.

### Route Design Guidance

- Design routes around **resources (nouns), not actions (verbs)**.
- Use **plural nouns** for collections (e.g., `/facilities`, `/notifications/{id}`).
- Keep routes **lowercase** and **hyphen-separated** (e.g., `/fhir-query-configuration`).
- Remove internal **component/service names** from internal routes; rely on routing/proxy layers externally if needed.
- Use **hierarchical structure** to express relationships (e.g., `/facilities/{facilityId}/configs`).
- Avoid embedding implementation details (e.g., database keys, technology names) in routes.
- Keep routes stable; introduce **versioning in the URL** (e.g., `/api/v1/...`) only for breaking changes.
- Use **query parameters** for filtering, sorting, and pagination (e.g., `?pageSize=10&pageNumber=1&sortBy=name`).
- Do not overload routes for UI concerns; prefer consistent resource representations over UI-specific endpoints.
- All services should have a base route of `/api/<servicename>`

### Input Sanitization

For any `string` query parameter on a REST endpoint, run it through `HtmlInputSanitizer.Sanitize()` or `SanitizeAndRemove()` before passing to business logic. Logging-message arguments must also be sanitized before reaching the logger to clear SAST findings.

### CancellationToken Plumbing

Async methods reachable from HTTP endpoints, Kafka listeners, Quartz jobs, or `BackgroundService.ExecuteAsync` must accept a `CancellationToken` and forward it to every call that has a token overload (`HttpClient`, `DbContext.SaveChangesAsync`, EF async LINQ, `Stream.Read/WriteAsync`, `Task.Delay`, `SemaphoreSlim.WaitAsync`, Polly, etc.). Don't pass `CancellationToken.None` / `default` when an ambient token is in scope. A timeout-only `CancellationTokenSource` must be linked to the caller's token via `CreateLinkedTokenSource`. Long per-row/per-patient loops should at minimum call `ThrowIfCancellationRequested()` per iteration when the inner call has no token overload.

# Frameworks and Deployment Technologies

* MongoDB queries should comply with limitations imposed by deployments within Azure CosmosDB for MongoDB RU (i.e. don't use aggregate pipelines that call mapReduce, $collStats, $indexStats, etc.)

## Pull Requests

PR summaries/titles must begin with one of the following prefixes. This is enforced by `.github/workflows/pr-title-check.yml`, which is the authoritative list — keep this section in step with it.

* LEGLINK-XXX: <brief_summary>  — the current JIRA project, and the right choice for most work
* LNK-XXX: <brief_summary>      — the former project key, still accepted for older tickets
* LEGPROG-XXX: <brief_summary>
* TECH_DEBT: <brief_summary>
* DOCS: <brief_summary>
* SNYK: <brief_summary>

`Dops-` and `Bump` (with its trailing space) are also accepted, for DevOps and dependency-update automation respectively. Do not use them for hand-written PRs.

Note the format is exact: the ticket prefix is upper-case, followed by a colon and a **space**. A title such as `LEGLINK-912:Fix the thing` fails the check.

Pull requests that have "TECH_DEBT" in the title should only contain changes related to typos, unused code, linter/IDE suggestions, swagger specification updates and logging improvements. Documentation-only changes belong under `DOCS:`. These TECH_DEBT PRs must not affect core functionality. All PRs that are not considered technical debt must reference a JIRA ticket number in their summary, and include information on what testing was performed in the description of the PR.

The description of the PR must follow this template:

------
### 🛠️ Description of Changes
<description of changes here>

### 🧪 Testing Performed
<description of testing performed>

### 🧑‍🔬 Unit Testing
- [ ] I have written or updated unit tests to cover my changes

### 📓 Documentation Updated
<indication of documentation updated here>
------
