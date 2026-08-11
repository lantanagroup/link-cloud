## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

## General Guidance

* Logging messages in functionality that repeat excessively (such as consumption of ResourceNormalized events - which occurs for every resource the system process, millions of times) should be treated as DEBUG messages or the namespace/package should be ignored by default so that it does not produce excessive persisted log messages.
* The Scripts directory contains scripts used by developers and admins to help operate the system at runtime. These scripts must always abstract out sensitive variables into arguments. Arguments may default values to environment variables for ease-of use, when appropriate.
* Changes to entities that are persisted with EntityFramework must always have a migration created for them, which ideally supports both upgarding *and* downgrading (in the event of failed system updates in other services).

### Application Configuration

The **catalog** — which keys exist and what they mean — is `/app-config.yaml` in this repository. The **values**, one export per environment, live in the private `lantanagroup/link-cac` repository under `Config/`. Everything in `Scripts/AzureAppConfig/` runs from this repository's root and looks for those exports at `../link-cac/Config`; override with `LINK_CAC_CONFIG_DIR` or `--config-dir`.

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

`Dops-` and `Bump ` are also accepted, for DevOps and dependency-update automation respectively. Do not use them for hand-written PRs.

Note the format is exact: the ticket prefix is upper-case, followed by a colon and a **space**. A title such as `LEGLINK-912:Fix the thing` fails the check.

Pull requests that have "TECH_DEBT" in the title should only contain changes related to typos, unused code, linter/IDE suggestions, swagger specification updates, documentation (such as markdown files) and logging improvements. These TECH_DEBT PRs must not affect core functionality. All PRs that are not considered technical debt must reference a JIRA ticket number in their summary, and include information on what testing was performed in the description of the PR.

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
