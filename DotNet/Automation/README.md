
# Automation

`Automation` is the platform-agnostic foundation library for synthetic FHIR generation, deterministic expectation modeling, and reusable automation helpers.

It intentionally has **no dependency on Link-specific services**. Instead, host projects such as `Automation.Link`, `Automation.UI`, and `BackendE2ETests` compose it with environment-specific orchestration.

---

## What this project is

At a high level, `Automation` provides the engine to:

1. **Generate realistic synthetic patient datasets** in FHIR R4 transaction bundles
2. **Control expected measure eligibility** per patient
3. **Model what pipeline output should look like** from the generated inputs
4. **Upload generated data to FHIR servers** with dependency-safe ordering
5. **Expose reusable monitoring/config/output primitives** that hosts can wire into end-to-end flows

Think of this project as the reusable "simulation + expectation" core.

---

## Project structure

```
Automation (this project — no Link dependencies)
├── Generation/          — FHIR R4 bundle generation + expectation models + streaming pipeline
├── Helpers/             — output abstractions, retry, monitoring, diagnostics
├── Configuration/       — base config classes
└── measures/            — embedded measure definition bundles (JSON)
```

---

## End-to-end conceptual workflow

Although orchestration happens in host projects, `Automation` is designed around this lifecycle:

1. **Input selection**
   - choose patient count / cohorts / profiles / measures / seed
2. **Deterministic generation**
   - produce clinically coherent FHIR resources and transaction bundles
3. **Streaming upload**
   - upload each patient's data as it is generated (no full dataset in memory)
4. **Manifest construction**
   - build concrete generated resource-key and resource-count maps incrementally
5. **Acquisition + CQL reachability modeling**
   - determine which generated resources are expected to be acquired and expected to appear in final artifacts
6. **Validation support**
   - provide stable contracts and derived expectations that validators can compare against actual pipeline output

This design avoids brittle static baselines and favors deterministic, input-derived expectations.

---

## Generation deep dive (`Generation/`)

### `FhirBundleGenerator`

`FhirBundleGenerator` orchestrates deterministic synthetic FHIR R4 transaction bundle generation.

Core properties:

- **Scenario-driven**: each patient is assigned one of 16 clinical scenarios
- **Seed-deterministic**: same inputs yield the same resource graph
- **Measure-aware**: profile-driven generation can intentionally produce qualifying/non-qualifying cohorts
- **Multi-measure capable**: patients can be shaped for one or many measures
- **Chunked output**: bundles are split at 500 entries to stay within transaction limits

### `FhirGenerationPipeline`

`FhirGenerationPipeline` is the recommended entry point for large-scale generation. It wraps `FhirBundleGenerator`'s per-patient logic in a streaming pipeline that:

1. **Generates shared infrastructure first** (Organization, Location, Practitioners, Medications) and uploads it
2. **Processes patients concurrently** (bounded by `MaxConcurrentPatients = 4`) but uploads each patient's chunks **sequentially** to preserve FHIR resource dependency order (Patient → Encounter → Observations)
3. **Builds the `GenerationManifest` incrementally** — resource keys and counts are recorded from in-memory FHIR objects before serialization
4. **Runs `QueryPlanAcquisitionSimulator` per-patient** (when configured) before discarding FHIR data
5. **Retains no serialized JSON after upload** — prevents OOM conditions with large patient counts

#### Pipeline result

`FhirGenerationPipeline.PipelineResult` contains:
- Ordered patient IDs
- Fully-populated `GenerationManifest` (resource keys, counts, profiles, simulated acquisition results)
- Total bundle upload count

#### Memory model

Traditional generation (`FhirBundleGenerator.Generate()`) holds all patients' FHIR objects and serialized JSON in memory simultaneously. The pipeline processes one patient at a time per concurrent slot, serializes, uploads, and disposes — keeping memory proportional to `MaxConcurrentPatients — resources-per-patient` rather than `total-patients — resources-per-patient`.

### Clinical scenarios (16 total)

| # | Scenario | Service |
|---|---|---|
| 0 | Community-acquired pneumonia | General Medicine |
| 1 | Acute decompensated heart failure | Cardiology |
| 2 | Acute myocardial infarction | Cardiothoracic Surgery |
| 3 | COPD exacerbation | Pulmonology |
| 4 | Sepsis (urinary source) | General Medicine |
| 5 | Hip fracture | Orthopaedics |
| 6 | Acute renal failure | Nephrology |
| 7 | Ischaemic stroke | Neurology |
| 8 | Diabetic ketoacidosis (DKA) | Endocrinology |
| 9 | Gastrointestinal bleeding | Gastroenterology |
| 10 | Pulmonary embolism | Pulmonology |
| 11 | Acute pancreatitis | Gastroenterology |
| 12 | Cellulitis | General Medicine |
| 13 | Atrial fibrillation with RVR | Cardiology |
| 14 | Diabetic hypoglycemia | Endocrinology |
| 15 | Acute appendicitis | General Surgery |

### Clinical scenario eligibility

`ClinicalScenarioEligibility` determines which clinical scenarios qualify for each measure:

| Measure | Qualifying scenarios |
|---|---|
| ACH Monthly Initial Population | **All 16** — every inpatient encounter qualifies |
| ACH Daily Initial Population | **All 16** — same as Monthly |
| Glycemic Control Hypoglycemic | **Only #8 (DKA) and #14 (Diabetic Hypoglycemia)** — requires diabetes-related diagnosis |

This means **non-qualifying cohorts for ACH measures are not currently meaningful** — all clinical scenarios produce qualifying encounters. Non-qualifying is only meaningful for the Hypoglycemic measure.

### `ScenarioResourceMap`

Each scenario maps to coherent clinical subsets of global resource pools:

- medications
- procedures
- observations
- specimens
- imaging
- service requests
- comorbidities

Universal inpatient resources (e.g., vitals, CBC/BMP, prophylaxis/PRN baselines) are included across scenarios.

### Resource factories (`ResourceFactories/`)

Each FHIR type has two generation modes:

- `Generate(id, seed, ...)` — deterministic seed-based selection
- `Create(id, callerValues, ...)` — explicit caller-controlled construction

Factories include:

`PatientFactory`, `EncounterFactory`, `ConditionFactory`, `MedicationRequestFactory`, `MedicationFactory`, `MedicationAdministrationFactory`, `ObservationFactory`, `ProcedureFactory`, `DiagnosticReportFactory`, `ServiceRequestFactory`, `SpecimenFactory`, `CoverageFactory`, `ImagingStudyFactory`, `ImmunizationFactory`, `AllergyIntoleranceFactory`, `CareTeamFactory`, `CarePlanFactory`, `DocumentReferenceFactory`, `ProvenanceFactory`, `DeviceFactory`, `LocationFactory`, `OrganizationFactory`, `PractitionerFactory`, `CensusListFactory`.

### Reference linkage model

Resources are linked during generation (not patched after the fact), for example:

- `MedicationRequest.medication` ? `Reference(Medication/{id})`
- `MedicationAdministration.request` ? `Reference(MedicationRequest/{id})`
- `ImagingStudy.basedOn` ? `Reference(ServiceRequest/{id})`
- `Provenance.target` ? `Reference(DiagnosticReport/{id})`
- `ServiceRequest` → `Specimen` → `Observation` → `DiagnosticReport` chain

This yields a consistent patient graph that downstream processes can traverse deterministically.

### Clinical terminology source (`FhirGenerationCodes`)

Centralized code tables include SNOMED, ICD-10, RxNorm, LOINC, and CVX-backed selections for demographics, practitioners, scenario definitions, observations, medications, procedures, service requests, and related assets.

---

## FHIR data loading (`FhirDataLoader`)

`FhirDataLoader` handles uploading generated FHIR transaction bundles to a FHIR server.

### Upload modes

| Method | Behavior |
|---|---|
| `UploadBundlesParallelAsync` | Concurrent upload with bounded parallelism. Used when dependency order doesn't matter (e.g., independent patients in batch mode). |
| `UploadBundlesSequentiallyAsync` | Strict sequential upload. **Aborts on first failure** — logs remaining bundles as skipped and returns `false`. Used to preserve resource dependency order (Patient → Encounter → Observations). |

### Retry and tracking

- `PostBundleWithRetryAsync` — retries failed POSTs with configurable delay
- `TrackCreatedResources` — parses FHIR transaction response to record created resource IDs
- `WaitForFhirReadyAsync` — polls FHIR server health endpoint before starting uploads

The sequential upload abort behavior ensures that if a Patient bundle fails, dependent Encounter and Observation bundles are not sent (which would fail with referential integrity errors anyway), preserving a clean FHIR server state.

---

## Cohort and profile model

### `PatientCohortDefinition`

Compact cohort inputs that define a group of patients:

- `PatientCount` — how many patients to generate
- `MeasureEligibilities` — per-measure qualifying/non-qualifying map
- `EligibleClinicalScenarioIds` — which clinical scenarios to draw from (empty = all)
- `ResourcesPerPatientMin/Max` — resource count range

### `PatientProfile`

Expanded per-patient configuration produced by `PatientCohortDefinition.ExpandProfiles()`:

- Per-measure eligibility map
- Seed offset for deterministic generation
- Clinical scenario ID assignment (round-robin from eligible scenarios)
- Resource count (randomized within cohort's min/max range)

### Expansion flow

```
PatientCohortDefinition[] → ExpandProfiles(seed) → PatientProfile[]
                                                      │
                                            FhirBundleGenerator / FhirGenerationPipeline
                                                      │
                                            GenerationManifest (resource keys + expectations)
```

When `EligibleClinicalScenarioIds` is empty, the expansion falls back to **all** clinical scenarios from `FhirGenerationCodes.ClinicalScenarios`.

---

## Key domain objects and relationships

- `ProfiledMeasureType`
  - enum of supported profiled measures
- `ProfiledMeasureCatalog`
  - metadata map: measure display names and embedded bundle locations
- `MeasureEligibility`
  - `Qualifying` / `NonQualifying` eligibility marker
- `PatientProfile`
  - per-patient eligibility and scenario controls used by profile-driven generation
- `PatientCohortDefinition`
  - compact cohort inputs that can be expanded into explicit `PatientProfile` lists
- `GenerationManifest`
  - concrete generated-input manifest (resource keys/counts + derived expectation filters)

---

## Predictive expectation model

`GenerationManifest` is the project's core expectation object. It records what was generated and exposes helper logic for "what should appear downstream" calculations.

### Building the manifest

The manifest can be built two ways:

1. **Batch** — `GenerationManifest.Build(patientIds, profiles, bundles, ...)` processes retained bundle data after generation completes
2. **Incremental** — `GenerationManifest.IncrementalBuilder` accumulates entries per-patient during streaming pipeline execution, then finalizes via `Build()`

The incremental builder is used by `FhirGenerationPipeline` so manifest metadata is captured from in-memory FHIR objects before they are serialized and discarded.

### Manifest capabilities

- Stores per-patient `ResourceType/Id` keys and counts
- Tracks selected measure IDs and patient eligibility
- Supports acquired-type filtering and expected-in-artifact filtering
- Supports deterministic key-level prediction integration (`SimulatedAcquiredResourceKeysByPatient`)
- Supports per-resource CQL exclusion integration (`CqlFilteredResourceKeysByPatient`)
- Excludes pipeline-derived types (`MeasureReport`, `OperationOutcome`) from comparisons

### Related generation helpers

- `QueryPlanDefaults`
  - canonical default query-plan definition and acquired-type extraction
- `QueryPlanAcquisitionSimulator`
  - deterministic replay of parameter/reference query semantics against generated bundles
  - simulates which resources would be acquired by DataAcquisition for each patient
- `CqlResourceTypeExtractor`
  - extracts CQL-retrieved resource types from measure bundles
  - reachability roots include both population criteria and `supplementalData` criteria expressions (SDE roots)
- `CqlFilterSimulator`
  - measure-family profile architecture for per-resource CQL filtering
  - applies SDE `where` semantics at resource level (not just type-level reachability)
  - currently implemented:
    - ACH family (`NhsnAcuteCareHospitalMonthlyInitialPopulation`, `NhsnAcuteCareHospitalDailyInitialPopulation`) Condition filtering
    - Hypoglycemic family (`NhsnGlycemicControlHypoglycemicInitialPopulation`) Condition filtering
  - extensible via `ICqlFilterProfile` for additional resource families (Coverage, Observation, ServiceRequest, etc.)

Together, these allow host validators to compare actual pipeline artifacts against deterministic expectations derived from known inputs.

### Prediction semantics (important)

Expectation calculation is intentionally layered:

1. **Generated keys** (what we created)
2. **Acquired keys** (`QueryPlanAcquisitionSimulator`) — what DA should fetch
3. **Type reachability** (`CqlResourceTypeExtractor`) — which resource types CQL can retrieve
4. **Per-resource CQL filters** (`CqlFilterSimulator`) — rows excluded by SDE `where` conditions

Final expected ABS set = generated ∩ acquired ∩ reachable-types − per-resource-CQL-exclusions.

This preserves realistic data texture in generation while making predictions precise enough for validator/UI comparison.

---

## Helpers deep dive (`Helpers/`)

Core reusable primitives include:

- Output and reporting
  - `IAutomationOutput`
  - `ConsoleAutomationOutput`
  - `EventingAutomationOutput`
  - `TimestampedAutomationOutput`
- Reliability and polling
  - `RetryHelper`
  - `StatusPollingHelper`
  - `BackgroundMonitorLoop`
- Monitoring/event model
  - `TestRunMonitor`
  - `ILogScraper` / `IMessageBusMonitor`
  - `MonitorEventModels` / `MonitorProbeModels`
  - `MilestoneTracker`
  - `ProgressTracker`
- Diagnostics/utilities
  - `DatabaseConnectionFactory`
  - `DiagnosticSnapshotWriter`
  - `ValidationRunner`

These are framework-level utilities consumed by higher-level orchestrators in host projects.

---

## Configuration model (`Configuration/`)

- `AutomationConfigBase`
  - common runtime settings (FHIR endpoints, cleanup toggles, auth modes)
- `TestScenarioConfigBase`
  - scenario execution settings (measure inputs, patient IDs, polling/timeouts)
- `OAuthConfig`
  - OAuth2 client-credentials configuration
- `BasicAuthConfig`
  - basic-auth credentials configuration

Host projects extend these base classes for environment/service-specific needs.

---

## How `Automation` fits into the wider ecosystem

`Automation` is consumed by:

- `Automation.Link`
  - adds Link service clients, facility/report orchestration, and pipeline validators
- `Automation.UI`
  - interactive Razor UI host for running and monitoring scenarios
- `BackendE2ETests`
  - automated test-host composition

This layering keeps generation/expectation logic reusable and testable without infrastructure coupling.

---

## Notes

- Targets `.NET 8`.
- Has no dependency on Link-specific projects such as `Shared` or `LinkSdk`.
- Uses `Hl7.Fhir.R4` for FHIR model types and `System.Text.Json` for serialization.
- `FhirGenerationPipeline` is the recommended entry point for large datasets; `FhirBundleGenerator.Generate()` is suitable for small/test datasets where holding everything in memory is acceptable.
- `UploadBundlesSequentiallyAsync` aborts on first failure to preserve resource dependency ordering guarantees.
