# MockFhirServer

`MockFhirServer` is a lightweight .NET 8 Minimal API that acts as a drop-in replacement for
a real FHIR R4 server (e.g. HAPI) during local development and integration testing. Instead
of storing or proxying real patient data, it generates synthetic FHIR R4 resources on the fly
using the deterministic data-generation engine from the `Automation` library, then discards
them immediately. Memory usage stays flat regardless of how many patients are queried.

---

## 1. What it is for

DataAcquisition queries a FHIR server over HTTP using the [Firely .NET SDK](https://github.com/FirelyTeam/firely-net-sdk)
(`FhirClient`). In a full deployment that server is HAPI or an EHR system. In local and CI
environments you can point DataAcquisition at `MockFhirServer` instead:

```
FhirQueryConfiguration.FhirServerBaseUrl = "http://mock-fhir-server:8080/fhir"   # Docker
FhirQueryConfiguration.FhirServerBaseUrl = "http://localhost:58354/fhir"           # local
```

The server responds to every read and search request with realistic, internally consistent
FHIR R4 data. The same patient ID always produces the same clinical data, so test results
are reproducible.

---

## 2. How it works

### Shared infrastructure (cached)

At startup the server calls `FhirBundleGenerator.BuildSharedResources()` once to build the
run-scoped shared resources:

| Resource type   | Description                                         |
|-----------------|-----------------------------------------------------|
| Organization    | One hospital organization                           |
| Location        | Inpatient, ICU, ED, step-down, outpatient locations |
| Practitioner    | A pool of attending and admitting practitioners     |
| Medication      | A formulary of common medications                   |
| Device          | Pulse oximeter, ventilator, CPAP devices            |

These ~200 objects are kept in memory for the lifetime of the process because their IDs must
remain stable for cross-patient references (e.g. `Encounter.location.reference`).

### Patient resources (never cached)

When DataAcquisition requests patient-specific data the server calls
`FhirBundleGenerator.GeneratePatientEntries()` with a `FhirGenerationConfig` scoped to the
requested resource type, so only resources of that type (plus the fixed clinical anchors —
Patient, Encounter, Condition, Device, CareTeam, CarePlan) are generated. The generated
objects are returned in the response and then immediately discarded. The generation is fully
deterministic: the patient ID is hashed with FNV-1a to produce a stable integer seed, so the
same patient ID always yields the same encounters, observations, conditions, etc.

This means:

- Memory usage is **O(shared resources)** regardless of patient population size.
- A patient can be queried repeatedly and always returns identical data.
- No persistent storage is required.

---

## 3. Supported FHIR operations

| Method | URL pattern                           | Notes                                               |
|--------|---------------------------------------|-----------------------------------------------------|
| GET    | `/fhir/metadata`                      | Returns a minimal `CapabilityStatement` (R4)        |
| GET    | `/fhir/{ResourceType}/{id}`           | Read. Patients are generated on demand.             |
| GET    | `/fhir/{ResourceType}?patient=...`    | Patient-scoped search. Accepts `patient` or `subject`. Reference format `Patient/{id}` or bare `{id}`. |
| GET    | `/fhir/{ResourceType}?_id=...`        | Multi-ID lookup. Comma-separated IDs.               |
| POST   | `/fhir/{ResourceType}/_search`        | Form-encoded POST search (used by `FhirClient.SearchUsingPostAsync`). Supports the same parameters as GET search. |
| GET    | `/health`                             | Health probe. Returns `{"status":"Healthy"}`.       |
| GET    | `/api/mock-fhir/patients`             | Returns pre-registered patient IDs, one per line (plain text). Use this to seed a census list. |

Paging is not implemented. All matching resources are returned in a single `searchset` Bundle.
DataAcquisition follows `Bundle.link[next]` for paging; when no next link is present it stops,
so the absence of paging links is handled correctly.

---

## 4. Configuration

All settings live under the `MockFhirServer` key in `appsettings.json` /
`appsettings.Docker.json` or as environment variables using the `__` separator
(e.g. `MockFhirServer__ResourcesPerPatient=200`).

| Setting                  | Type     | Default | Description |
|--------------------------|----------|---------|-------------|
| `PreGeneratedPatientCount` | int    | `10`    | Number of patient IDs registered at startup and exposed by `/api/mock-fhir/patients`. No data is pre-generated; this just pre-registers the IDs. |
| `ResourcesPerPatient`    | int      | `100`   | Total FHIR resources generated per patient per request, distributed across resource types (Observation, Condition, Procedure, etc.). |
| `GenerationSeed`         | int?     | `null`  | Base seed for the pre-registered patient IDs. `null` means patient IDs are `mock-patient-0001`, `mock-patient-0002`, … starting at index 1. Set to a fixed integer for fully reproducible ID sequences across restarts. |
| `ClinicalPeriodStart`    | string?  | `null`  | ISO 8601 date-time bounding the start of generated encounter windows. Example: `"2024-01-01T00:00:00Z"`. When set together with `ClinicalPeriodEnd`, all encounter dates will fall within the window, ensuring date-filtered FHIR queries return data. |
| `ClinicalPeriodEnd`      | string?  | `null`  | ISO 8601 date-time bounding the end of generated encounter windows. |

### Choosing `ResourcesPerPatient`

The Automation library distributes resources according to `FhirGenerationConfig.ResourceDistribution`
(roughly 28 % Observations, 8 % Conditions, etc.). A value of `100` generates about 28
observations, 8 conditions, and so on per patient. Increase this if a measure requires a
higher volume of a particular resource type to satisfy its initial population criteria.

### Aligning encounter dates with a reporting period

DataAcquisition passes date range parameters when querying Encounters
(`date=ge{start}&date=le{end}`). If the mock server generates encounters outside that range,
queries return empty bundles. Configure `ClinicalPeriodStart` and `ClinicalPeriodEnd` to match
the reporting period used in the census/facility configuration:

```json
"MockFhirServer": {
  "ClinicalPeriodStart": "2024-01-01T00:00:00Z",
  "ClinicalPeriodEnd":   "2024-01-31T23:59:59Z"
}
```

---

## 5. Typical developer workflow

### Step 1 — Start the mock server with Docker Compose

```bash
docker compose up -d mock-fhir-server
```

The server listens on port **58354** externally (`8080` inside the container).

### Step 2 — Get pre-registered patient IDs

```bash
curl http://localhost:58354/api/mock-fhir/patients
# mock-patient-0001
# mock-patient-0002
# ...
```

Use these IDs as `PatientId` values when creating a census entry or a DataAcquisition query
request. Any other patient ID also works — new IDs are accepted on first request.

### Step 3 — Point DataAcquisition at the mock server

In the facility's `FhirQueryConfiguration`, set:

```json
{
  "FhirServerBaseUrl": "http://mock-fhir-server:8080/fhir"
}
```

(or `http://localhost:58354/fhir` when running DataAcquisition outside Docker).

### Step 4 — Run a report

Trigger a data acquisition run as normal. The mock server will generate FHIR data for each
patient on demand.

### Step 5 — Browse the Swagger UI

While the server is running, navigate to [http://localhost:58354/swagger](http://localhost:58354/swagger)
to inspect and manually invoke all endpoints.

---

## 6. Resource ID convention

Generated resource IDs follow the pattern:

```
{patientId}-{TypeAbbrev}-{3-digit-index}
```

Examples: `mock-patient-0001-Obs-007`, `mock-patient-0001-Enc-001`.

This convention is what allows the server to regenerate the correct patient's data when
DataAcquisition does a direct read on a resource it received from an earlier search result.

Shared resource IDs are generated from a run-scoped tag (`SharedIds.RunTag`) and do **not**
include a patient ID prefix. These IDs change on each process restart.

---

## 7. Project structure

```
DotNet/MockFhirServer/
├── Program.cs                        # Minimal API endpoints
├── MockFhirServer.csproj
├── appsettings.json                  # Local defaults
├── appsettings.Docker.json           # Docker overrides
├── Dockerfile
├── Settings/
│   └── MockFhirServerSettings.cs     # Typed configuration
└── Services/
    └── PatientDataStore.cs           # Shared resource cache + on-demand generation
```

Key dependencies:

| Dependency                   | Role |
|------------------------------|------|
| `Automation` (project ref)   | `FhirBundleGenerator` — shared infra and per-patient entry generation |
| `Hl7.Fhir.R4` v5             | FHIR model types and `FhirJsonSerializer` |
| `Swashbuckle.AspNetCore` v6  | Swagger UI |
