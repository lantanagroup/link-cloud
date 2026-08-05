## Overview

This system is a high-performing, big-data platform intended to support collecting data directly from multiple hospitals/facilities/EHRs, normalizing the data (when minimally necessary), evaluating it using measure/CQL logic, and submitting the data to down-stream systems in a well-defined structure/format.

## Technology Stack

### .NET Services (C# / .NET 8)
- ASP.NET Core Web APIs with Swagger/OpenAPI
- Entity Framework Core 8 (SQL Server primary, MongoDB for document storage)
- Confluent.Kafka for event messaging
- Quartz.NET for job scheduling
- MediatR for CQRS command/query dispatching
- FluentValidation for input validation
- Serilog for structured logging (Loki sink)
- OpenTelemetry for distributed tracing and metrics
- xUnit + Moq + FluentAssertions for unit tests; SpecFlow for BDD; Testcontainers for integration tests

### Java Services (Spring Boot)
- MeasureEval (CQL/measure evaluation) and Validation (FHIR validation)
- Separate Maven modules; shared module published to a private Maven repository
- `settings.xml` uses environment variables for repository credentials

### Frontend
- Admin.UI: React/TypeScript (Angular-style component structure)
- Automation.UI: .NET Blazor/Razor-based

## Architecture

### Service Map

| Service | Port | Language | Responsibility |
|---------|------|----------|---------------|
| Account | 8060 | C# | User account management |
| Admin.BFF | 8063 | C# | Backend-for-Frontend for Admin UI |
| Admin.UI | 8066 | React | Admin dashboard |
| Audit | 8062 | C# | Audit event logging |
| Census | 8064 | C# | Patient census/population management |
| DataAcquisition | 8065 | C# | FHIR data ingestion API |
| DataAcquisition.AcquisitionWorker | 8078 | C# | Async data acquisition processing |
| MeasureEval | 8067 | Java | CQL measure evaluation |
| Normalization | 8068 | C# | Data normalization engine |
| QueryDispatch | 8071 | C# | Query routing and dispatch |
| Report | 8072 | C# | Report generation |
| Submission | 8073 | C# | Data submission to external systems |
| Tenant | 8074 | C# | Multi-tenancy management |
| Terminology | 8076 | C# | Terminology/code system lookups |
| Validation | 8075 | Java | FHIR resource validation |

### Infrastructure Dependencies
- **SQL Server 2022** (port 1433) — primary relational store
- **MongoDB 6** (port 17017) — document store (replica set required)
- **Kafka 3.4** (port 9092) — event streaming with SASL/PLAIN auth
- **Redis 6.2** (port 6379) — caching
- **Azurite** (port 10000) — Azure Blob Storage emulation locally
- **Observability**: Prometheus, Grafana, Loki, Tempo, OpenTelemetry Collector

### Event-Driven Data Flow

Kafka topics follow the pattern `TopicName`, `TopicName-Error`, `TopicName-Retry` (defined in `topics.txt`). Key event pipeline:

```
PatientCensusScheduled → DataAcquisitionRequested → ResourcesAcquired → ResourcesNormalized → EvaluationRequested → GenerateReportRequested → SubmitPayload → PayloadSubmitted
```

Each service consumes from its topic and produces to the next. Failed messages route to `-Error`; retryable failures route to `-Retry`.

### Inter-Service Communication
- **Kafka** for async event-driven workflows
- **REST/HTTP** for synchronous queries (via `DotNet/LinkSdk` — Flurl-based typed clients)
- **gRPC** where performance-critical communication is needed

## Project Structure

```
DotNet/
  Shared/              # Common libraries (extensions, models, Kafka abstractions)
  LinkSdk/             # Typed HTTP clients for inter-service calls (Flurl-based)
  Link.Authorization/  # Auth middleware and policies
  <ServiceName>/       # Individual microservice projects
Java/
  shared/              # Common Java libraries
  measureeval/         # CQL measure evaluation service
  validation/          # FHIR validation service
Web/
  Admin.UI/            # React frontend
Tests/
  BackendE2ETests/     # End-to-end API tests over Automation.UI
  ServiceTests/        # Service-level integration tests
Scripts/               # Deployment & operations scripts (aca-*, k8s-*, docker-compose.*)
Azure_Pipelines/       # CI/CD pipeline definitions per service
```

## General Guidance

* Logging messages in functionality that repeat excessively (such as consumption of ResourceNormalized events - which occurs for every resource the system process, millions of times) should be treated as DEBUG messages or the namespace/package should be ignored by default so that it does not produce excessive persisted log messages.
* The Scripts directory contains scripts used by developers and admins to help operate the system at runtime. These scripts must always abstract out sensitive variables into arguments. Arguments may default values to environment variables for ease-of use, when appropriate.
* Changes to entities that are persisted with EntityFramework must always have a migration created for them, which ideally supports both upgarding *and* downgrading (in the event of failed system updates in other services).

### .NET Service Startup Pattern

All .NET services follow the same `Program.cs` structure (see `DotNet/Census/Program.cs` as a canonical example):

1. Register configuration (`IOptions<T>` bindings from `app-config.yaml` keys)
2. Register EF DbContext with `AutoMigrateEF<T>()` (runs migrations on startup)
3. Register Kafka consumer/producer factories (`IKafkaConsumerFactory`, `IKafkaProducerFactory`)
4. Register health checks (Kafka + database connectivity)
5. Configure authentication/authorization middleware
6. Configure OpenTelemetry (tracing + metrics)
7. Map controller endpoints and start Kafka consumer listeners

### Configuration

All service configuration keys are defined in `app-config.yaml` (root). Global keys (Kafka, database, auth) are shared; service-specific keys are nested under `services.<name>`. Configuration binds via `IOptions<T>` pattern using colon-separated key paths (e.g., `KafkaConnection:BootstrapServers`).

### Entity Framework Migrations

- Migrations auto-run on startup via `AutoMigrateEF<T>()` extension (`DotNet/Shared/Application/Extensions/EFMigrations.cs`)
- To add a migration: `dotnet ef migrations add <Name> --project <ServiceProject>`
- Always provide both `Up()` and `Down()` methods

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

## Building and Running

### Local Development (Docker Compose)

```powershell
docker-compose up -d    # Start all infrastructure + services
```

The `docker-compose.yml` in root defines all services and infrastructure. Individual services can be run from IDE with configuration pointing to local infrastructure.

### .NET Services

```powershell
dotnet build DotNet/<ServiceName>
dotnet run --project DotNet/<ServiceName>
```

### Java Services

```powershell
cd Java/<servicename>
mvn clean package
```

Java shared module must be built/published first if modified.

### Running Tests

```powershell
# Unit tests (per service)
dotnet test DotNet/<ServiceName>.Tests

# E2E tests (require running infrastructure)
dotnet test Tests/BackendE2ETests
```

E2E tests are categorized: `PR` (fast, safe for CI) and `LongRunning` (full workflow scenarios).

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
