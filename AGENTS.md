## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

Link Cloud is an open-source, event-driven microservices platform for FHIR-based public health reporting. It ingests clinical data from EHRs, normalizes it, evaluates measures (CQL), validates, and submits results downstream. Services span three runtimes that all share one Kafka bus, one set of databases, and one local docker-compose stack:

- **.NET 8** services under `DotNet/` (most services) — ASP.NET minimal APIs, EF Core or MongoDB, Kafka via Confluent.Kafka.
- **Java 17 / Spring Boot 3** services under `Java/` (`measureeval`, `validation`, plus a `shared` lib and the HAPI FHIR test server).
- **Angular** Admin UI under `Web/Admin.UI/`.

The pipeline is roughly: `Census → DataAcquisition → Normalization → MeasureEval (Java) → Validation (Java) → Report → Submission`, glued together by Kafka topics in `topics.txt`. `QueryDispatch`, `Tenant`, `Account`, `Audit`, `Notification`, `Terminology`, `Admin.BFF` are supporting services.

## Building, running and testing

See **[DEVELOPMENT.md](DEVELOPMENT.md)** for the full set: the local docker-compose
stack, `dotnet build`/`test` for the solution or one service, the Backend E2E suites, the Maven
builds under `Java/`, the Admin UI, and EF Core migrations.

Two things that catch people out and are worth knowing without opening it:

- Integration tests need Docker running (Testcontainers brings up SQL Server and Azurite), and
  the Backend E2E suites need the compose stack already up and healthy.
- Any entity change persisted through EF Core must ship a migration supporting **both** upgrade
  and downgrade.

## Architecture

See **[ARCHITECTURE.md](ARCHITECTURE.md)**. It covers the five-layer .NET service
shape, `DotNet/Shared` as the contract layer, Kafka topic and key conventions, the configuration
registry, the automation and E2E projects, the report-generation pipeline end to end, MongoDB
usage, the internal/external blob trust boundary, and the known latent issues.

Read it before changing anything that crosses a service boundary. Four facts from it that a
plausible-looking change tends to get wrong:

- **FHIR resource bodies do not travel on Kafka.** They go through a shared cache; the events
  carry cache pointers, and there are no per-resource messages.
- **MeasureEval runs in two phases** and can emit a *new* `DataAcquisitionRequested`, so the
  pipeline is not a straight line.
- **No service inserts into `link-report` except Report itself** — every later stage updates rows
  that were pre-allocated.
- **There is no submission call.** Copying a file from the internal to the external blob
  container *is* the submission.

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
