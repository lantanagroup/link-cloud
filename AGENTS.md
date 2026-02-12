## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

## General Guidance

* Logging messages in functionality that repeat excessively (such as consumption of ResourceNormalized events - which occurs for every resource the system process, millions of times) should be treated as DEBUG messages or the namespace/package should be ignored by default so that it does not produce excessive persisted log messages.
* The Scripts directory contains scripts used by developers and admins to help operate the system at runtime. These scripts must always abstract out sensitive variables into arguments. Arguments may default values to environment variables for ease-of use, when appropriate.
* Changes to entities that are persisted with EntityFramework must always have a migration created for them, which ideally supports both upgarding *and* downgrading (in the event of failed system updates in other services).

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
    - Return 204 if no results.
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

PR summaries/titles must follow one of the two formats:
* TECH_DEBT: <brief_summary>
* LNK-XXX: <brief_summary>

Pull requests that have "TECH_DEBT" in the title should only contain changes related to typos, unused code, linter/IDE suggestions, swagger specification updates, documentation (such as markdown files) and logging improvements. These TECH_DEBT PRs must not affect core functionality. All PRs that are not considered technical debt must reference a JIRA ticket number in their summary for the LNK project, and include information on what testing was performed in the description of the PR.

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
