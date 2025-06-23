[← Back Home](../README.md)

# Data Acquisition Overview

The Data Acquisition subsystem consists of two main services: **DataAcquisition** (API/service) and **DataAcquisition.Worker** (background worker). Together, they orchestrate the retrieval, processing, and persistence of clinical data from external FHIR endpoints, supporting the Link reporting pipeline.

## Service Responsibilities

### DataAcquisition (API/Service)
- Exposes REST endpoints for managing query plans, logs, and configuration.
- Listens for Kafka events (e.g., `DataAcquisitionRequested`) to initiate data retrieval.
- Coordinates the creation and scheduling of acquisition jobs.
- Provides endpoints for monitoring and troubleshooting (e.g., log queries, query plan configs).

### DataAcquisition.Worker
- Runs as a background service, processing acquisition jobs from the queue.
- Listens for `ReadyToAcquire` Kafka events and executes FHIR queries as defined in the query plan.
- Handles retries, error handling, and dead-letter scenarios for failed acquisitions.
- Persists acquired FHIR resources and logs acquisition results.

## Workflow

1. **Triggering Acquisition**
   - The process begins with a Kafka event (e.g., `DataAcquisitionRequested` or `PatientCensusScheduled`).
   - The API service schedules acquisition jobs and emits `ReadyToAcquire` events.

2. **Job Processing**
   - The Worker service listens for `ReadyToAcquire` events.
   - For each event, it executes the relevant FHIR queries (using `FhirApiService`, `ReadFhirCommand`, `SearchFhirCommand`).
   - Results are persisted and acquisition logs are updated.

3. **Error Handling & Retries**
   - Both services use shared error handling and retry logic.
   - Failed jobs are retried or sent to a dead-letter queue as appropriate.

4. **Progressive Querying**
   - The system supports progressive querying: initial data is acquired for measure evaluation, and supplemental data is fetched as needed.
   - Query plans (daily, weekly, monthly, discharge) define the scope and timing of acquisitions.

## Configuration

- **Query Plans**: Define what data to acquire and when (daily, weekly, monthly, discharge).
- **Query Configs**: Specify FHIR server endpoints, authentication, and resource types.
- **Throttling**: Configurable limits to respect EHR/FHIR server rate limits.
- **Tenant/Facility**: All configuration is tenant-aware and supports multiple facilities.

## Key Classes & Components

- `DataAcquisitionRequestedListener`, `ReadyToAcquireListener`: Listen for Kafka events and trigger jobs.
- `AcquisitionProcessingJob`: Executes the acquisition logic for a batch of patients/resources.
- `FhirApiService`, `ReadFhirCommand`, `SearchFhirCommand`: Encapsulate FHIR API interactions.
- `DataAcquisitionLogService`, `DataAcquisitionLogManager`: Handle logging and persistence of acquisition results.
- `QueryPlanConfigController`, `LogController`: API endpoints for configuration and monitoring.

## Monitoring & Troubleshooting

- Acquisition logs are accessible via API endpoints for auditing and troubleshooting.
- All times are stored in UTC; tenant time zones are respected for scheduling.
- Dead-letter and retry topics are used for robust error handling.

---

This architecture ensures efficient, reliable, and auditable data acquisition, supporting both scheduled and event-driven reporting needs in a scalable, multi-tenant environment.