# Automation

`Automation` is the platform-agnostic foundation library for deterministic synthetic FHIR
generation, predictive pipeline modeling, and reusable automation primitives. It has no
dependency on any Link-specific service; host projects compose it with environment-specific
orchestration.

This README is the comprehensive reference for the project. It is aimed at three audiences:

- **Product owners / project managers** -- sections 1 and 2 explain what the project produces
  and why it exists.
- **QA** -- sections 3 and 4 explain how generation is configured and what determinism
  guarantees are in place.
- **Developers** -- sections 5 through 9 walk through every extensible surface and the
  mathematical model that drives predictions.

---

## 1. What this project is

At a high level, `Automation` provides the engine to:

1. **Generate realistic synthetic patient datasets** in FHIR R4 transaction bundles.
2. **Control expected measure eligibility** per patient.
3. **Model what pipeline output should look like** given the generated inputs.
4. **Upload generated data to FHIR servers** with dependency-safe ordering.
5. **Expose reusable monitoring/config/output primitives** that any host can wire into an
   end-to-end flow.

Think of this project as the reusable **simulation + expectation** core. Host projects
(`Automation.Link`, `Automation.UI`, `BackendE2ETests`) layer orchestration, Link service
clients, and presentation on top.

---

## 2. Conceptual workflow

Although orchestration happens in host projects, `Automation` is designed around this lifecycle:

1. **Input selection** -- choose patient count, cohorts, profiles, measures, seed.
2. **Deterministic generation** -- produce clinically coherent FHIR resources and transaction
   bundles.
3. **Streaming upload** -- upload each patient's data as it is generated (no full dataset in
   memory).
4. **Manifest construction** -- build concrete generated resource-key and resource-count maps
   incrementally.
5. **Acquisition + CQL reachability modeling** -- determine which generated resources are
   expected to be acquired and to appear in final artifacts.
6. **Validation support** -- expose stable contracts and derived expectations that validators can
   compare against actual pipeline output.

This design avoids brittle static baselines and favors deterministic, input-derived
expectations.

---

## 3. Project layout

```
Automation (no Link dependencies)
+-- Generation/          FHIR R4 bundle generation, prediction model, streaming pipeline
+-- Helpers/             output abstractions, retry, monitoring, diagnostics
+-- Configuration/       base config classes for host extension
+-- ExtractCqlTypes/     tiny utility app for extracting CQL retrieve types from measure bundles
+-- measures/            embedded measure definition bundles (JSON)
```

---

## 4. Generation deep dive (`Generation/`)

### 4.1 `FhirBundleGenerator`

Orchestrates deterministic synthetic FHIR R4 transaction bundle generation. Core properties:

- **Scenario-driven** -- each patient is assigned one of 16 clinical scenarios.
- **Seed-deterministic** -- same inputs always yield the same resource graph.
- **Measure-aware** -- profile-driven generation can intentionally produce qualifying and
  non-qualifying cohorts per measure.
- **Multi-measure capable** -- patients can be shaped for one or many measures.
- **Chunked output** -- bundles are split at 500 entries to stay within FHIR transaction limits.

Suitable for small test datasets where holding everything in memory is acceptable.

### 4.2 `FhirGenerationPipeline`

Recommended entry point for all non-trivial runs. Wraps `FhirBundleGenerator`'s per-patient
logic in a streaming pipeline that:

1. **Generates shared infrastructure first** (Organization, Location, Practitioners, Medications)
   and uploads it.
2. **Processes patients concurrently** (bounded by `MaxConcurrentPatients = 4`) but uploads
   each patient's chunks **sequentially** to preserve FHIR resource dependency order
   (Patient -> Encounter -> Observations).
3. **Builds the `GenerationManifest` incrementally** -- resource keys and counts are recorded
   from in-memory FHIR objects before serialization.
4. **Runs `QueryPlanAcquisitionSimulator` per-patient** (when configured) before discarding
   FHIR data.
5. **Runs `CqlFilterSimulator` per-patient** over the patient's *qualifying* measures only, so
   non-qualifying measure SDE rules do not falsely constrain prediction.
6. **Retains no serialized JSON after upload** -- memory stays proportional to
   `MaxConcurrentPatients -- resources-per-patient`, not the full run size.

`FhirGenerationPipeline.PipelineResult` contains ordered patient IDs, a fully populated
`GenerationManifest` (keys, counts, profiles, simulated acquisition results, CQL exclusions),
and the total bundle upload count.

### 4.3 Clinical scenarios (16 total)

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

### 4.4 Clinical scenario eligibility (`ClinicalScenarioEligibility`)

| Measure | Qualifying scenarios |
|---|---|
| ACH Monthly Initial Population | All 16 -- every inpatient encounter qualifies. |
| ACH Daily Initial Population | All 16 -- same as Monthly. |
| Glycemic Control Hypoglycemic | Only #8 (DKA) and #14 (Diabetic Hypoglycemia). |

Because ACH qualifies every inpatient encounter, "non-qualifying for ACH" is not currently a
meaningful cohort state -- all clinical scenarios produce qualifying encounters. Non-qualifying
is meaningful only for the Hypoglycemic measure (which is why `MultiMeasureTest` has an
ACH-qualifying-but-Hypo-non-qualifying cohort).

### 4.5 `ScenarioResourceMap`

Each scenario maps to coherent clinical subsets of global resource pools (medications,
procedures, observations, specimens, imaging studies, service requests, comorbidities).
Universal inpatient resources (vitals, CBC/BMP, prophylaxis/PRN baselines) are added across all
scenarios.

### 4.6 Resource factories (`ResourceFactories/`)

Each FHIR resource type has two generation modes:

- `Generate(id, seed, ...)` -- deterministic seed-based selection.
- `Create(id, callerValues, ...)` -- explicit caller-controlled construction.

Factories: `PatientFactory`, `EncounterFactory`, `ConditionFactory`, `MedicationRequestFactory`,
`MedicationFactory`, `MedicationAdministrationFactory`, `ObservationFactory`, `ProcedureFactory`,
`DiagnosticReportFactory`, `ServiceRequestFactory`, `SpecimenFactory`, `CoverageFactory`,
`ImagingStudyFactory`, `ImmunizationFactory`, `AllergyIntoleranceFactory`, `CareTeamFactory`,
`CarePlanFactory`, `DocumentReferenceFactory`, `ProvenanceFactory`, `DeviceFactory`,
`LocationFactory`, `OrganizationFactory`, `PractitionerFactory`, `CensusListFactory`.

### 4.7 Reference linkage

Resources are linked during generation (not patched after), so the bundles contain a coherent
patient graph that downstream processes traverse deterministically. Examples:

- `MedicationRequest.medication` -- `Reference(Medication/{id})`
- `MedicationAdministration.request` -- `Reference(MedicationRequest/{id})`
- `ImagingStudy.basedOn` -- `Reference(ServiceRequest/{id})`
- `Provenance.target` -- `Reference(DiagnosticReport/{id})`
- `ServiceRequest` -> `Specimen` -> `Observation` -> `DiagnosticReport` chain.

### 4.8 Clinical terminology source (`FhirGenerationCodes`)

Central code tables covering SNOMED, ICD-10, RxNorm, LOINC, and CVX selections for
demographics, practitioners, scenario definitions, observations, medications, procedures,
service requests, and related artifacts.

---

## 5. FHIR data loading (`FhirDataLoader`)

`FhirDataLoader` uploads generated transaction bundles to a FHIR server.

| Method | Behavior |
|---|---|
| `UploadBundlesParallelAsync` | Concurrent upload with bounded parallelism. Used when dependency order is irrelevant (e.g., independent patients in batch mode). |
| `UploadBundlesSequentiallyAsync` | Strict sequential upload. **Aborts on first failure** -- logs remaining bundles as skipped and returns `false`. Preserves resource dependency order (Patient -> Encounter -> Observations). |

Support methods:

- `PostBundleWithRetryAsync` -- retries failed POSTs with configurable delay.
- `TrackCreatedResources` -- parses FHIR transaction response to record created resource IDs.
- `WaitForFhirReadyAsync` -- polls FHIR server health endpoint before starting uploads.

The sequential upload abort behavior is intentional: if a Patient bundle fails, dependent
Encounter and Observation bundles would fail with referential integrity errors anyway. Aborting
early keeps the server in a clean state.

---

## 6. Cohort and profile model

### 6.1 `PatientCohortDefinition`

Compact cohort inputs defining a group of patients:

- `PatientCount` -- how many patients to generate.
- `MeasureEligibilities` -- per-measure `Qualifying` / `NonQualifying` map.
- `EligibleClinicalScenarioIds` -- which clinical scenarios to draw from (empty = all).
- `ResourcesPerPatientMin` / `ResourcesPerPatientMax` -- resource count range.

### 6.2 `PatientProfile`

Expanded per-patient configuration produced by `PatientCohortDefinition.ExpandProfiles()`:

- Per-measure eligibility map.
- Seed offset for deterministic generation.
- Clinical scenario assignment (round-robin from eligible scenarios).
- Resource count (randomized within the cohort's min/max range).

### 6.3 Expansion flow

```
PatientCohortDefinition[]
    +-> ExpandProfiles(seed)
            +-> PatientProfile[]
                    +-> FhirBundleGenerator / FhirGenerationPipeline
                            +-> GenerationManifest (keys + predictions)
```

When `EligibleClinicalScenarioIds` is empty, expansion falls back to all clinical scenarios
from `FhirGenerationCodes.ClinicalScenarios`.

---

## 7. Predictive expectation model

`GenerationManifest` is the project's core expectation object. It records what was generated
and exposes helper logic for "what should appear downstream" calculations.

### 7.1 Building the manifest

Two construction modes:

1. **Batch** -- `GenerationManifest.Build(patientIds, profiles, bundles, ...)` processes retained
   bundle data after generation completes.
2. **Incremental** -- `GenerationManifest.IncrementalBuilder` accumulates entries per patient
   during streaming pipeline execution, then finalizes. Used by `FhirGenerationPipeline` so
   manifest metadata is captured from in-memory FHIR objects before they are serialized and
   discarded.

### 7.2 Manifest state

- Per-patient `ResourceType/Id` keys and counts.
- Selected measure IDs and per-patient eligibility.
- Query-plan acquired resource types (+ parameter-query subset).
- CQL-referenced resource types (from `CqlResourceTypeExtractor`).
- `SimulatedAcquiredResourceKeysByPatient` -- deterministic key-level acquisition replay.
- `CqlFilteredResourceKeysByPatient` -- per-resource SDE exclusions.
- `ExpectedOperationOutcomeCountByPatient` -- post-hoc hook populated by
  `ReportAbsManifestValidator` from `ReportEntry.ReportingStatus`.

### 7.3 Prediction formula

For each patient the predicted set of resources is computed in layers:

```
base        = simulated-acquired keys (fallback: generated keys) filtered by
              IsExpectedInAbs(resourceType)
base        = base minus CqlFilteredResourceKeysByPatient[patientId]
base        = base plus Patient/{patientId}   when the patient qualifies for any measure
              (MeasureEval's CQL engine loads Patient implicitly)

derived +=  MeasureReport   : number of measures the patient qualifies for
derived +=  OperationOutcome: ExpectedOperationOutcomeCountByPatient[patientId]    (ABS only)
```

Final predicted resource-type counts = base counts + derived counts.

### 7.4 Two prediction destinations

`GenerationManifest` exposes two predictors that share the base math but diverge on
pipeline-derived additions:

| Method | Consumer | Derived additions |
|---|---|---|
| `GetExpectedAbsCountsForPatient(patientId)` | `ReportAbsManifestValidator` (patient NDJSON in ABS) | Patient, MeasureReport, **OperationOutcome** |
| `GetExpectedReportResourceCountsForPatient(patientId)` | `ReportDatabaseValidator` (`ReportResource` table) | Patient, MeasureReport (OperationOutcome excluded) |

OperationOutcome is excluded from the DB prediction because
`ValidationCompleteListener.ProcessMessageAsync` appends it directly to the ABS blob via
`PatientAggregator.AppendResourceToBlob(...)`, bypassing `PatientAggregator`'s aggregation +
`ReportResourceManager.AddAsyncWithAggregateResult` flow. It never reaches the table.

### 7.5 CQL filter simulator semantics

`CqlFilterSimulator.ComputeFilteredKeys(measures, input)` returns resources that will be
excluded by SDE `where` semantics. The result is the **intersection** of exclusions across
applicable measure profiles, because MeasureEval evaluates each measure independently and
writes one `.mr` file per measure; `PatientAggregator` unions contained resources across those
files into the patient NDJSON. A resource is only truly absent from ABS when every applicable
measure excludes it.

For multi-measure runs with per-patient eligibility, `FhirGenerationPipeline` passes only each
patient's **qualifying** measures into `ComputeFilteredKeys`, because a measure the patient
does not qualify for does not contribute contained resources to ABS and therefore must not
influence the intersection.

Current profiles:

- `AchConditionFilterProfile` -- `problem-list-item` requires `active` + `recordedDate <
  encounterEnd`; `encounter-diagnosis` / `health-concern` must reference the inpatient
  encounter.
- `HypoglycemicConditionFilterProfile` -- `recordedDate <= encounterEnd` (no active constraint).

Extensible via `ICqlFilterProfile` for additional families (Coverage, Observation,
ServiceRequest, etc.).

### 7.6 Related helpers

- `QueryPlanDefaults` -- canonical default query-plan definition and acquired-type extraction.
- `QueryPlanAcquisitionSimulator` -- deterministic replay of parameter/reference query
  semantics against generated bundles; simulates which resources DataAcquisition would acquire
  for each patient.
- `CqlResourceTypeExtractor` -- extracts CQL-retrieved resource types from measure bundles.
  Reachability roots include both population criteria expressions and `supplementalData`
  criteria expressions (SDE roots).
- `CqlFilterInputExtractor` -- walks generated entries to build the `PatientCqlInput` that the
  simulator consumes (Encounter period, Condition attributes, etc.).

Together, these allow host validators to compare actual pipeline artifacts against
deterministic expectations derived from known inputs.

---

## 8. Helpers (`Helpers/`)

Reusable framework-level primitives consumed by higher-level host orchestrators:

- **Output and reporting**
  `IAutomationOutput`, `ConsoleAutomationOutput`, `EventingAutomationOutput`,
  `TimestampedAutomationOutput`.
- **Reliability and polling**
  `RetryHelper`, `StatusPollingHelper`, `BackgroundMonitorLoop`.
- **Monitoring / event model**
  `TestRunMonitor`, `ILogScraper`, `IMessageBusMonitor`, `MonitorEventModels`,
  `MonitorProbeModels`, `MilestoneTracker`, `ProgressTracker`.
- **Diagnostics / utilities**
  `DatabaseConnectionFactory`, `DiagnosticSnapshotWriter`, `ValidationRunner`.

---

## 9. Configuration (`Configuration/`)

- `AutomationConfigBase` -- common runtime settings (FHIR endpoints, cleanup toggles, auth
  modes).
- `TestScenarioConfigBase` -- scenario execution settings (measure inputs, patient IDs, polling
  windows, timeouts).
- `OAuthConfig` -- OAuth2 client-credentials configuration.
- `BasicAuthConfig` -- basic-auth credentials configuration.

Host projects extend these base classes for environment- or service-specific needs.

---

## 10. How `Automation` fits into the wider ecosystem

`Automation` is consumed by:

- **`Automation.Link`** -- adds Link service clients, facility/report orchestration, pipeline
  validators, and monitoring probes.
- **`Automation.UI`** -- interactive Razor MVC host for running and monitoring scenarios.
- **`BackendE2ETests`** -- automated test-host composition.

This layering keeps generation and expectation logic reusable and testable without
infrastructure coupling.

---

## 11. Notes

- Targets `.NET 8`.
- No dependency on Link-specific projects such as `Shared` or `LinkSdk`.
- Uses `Hl7.Fhir.R4` for FHIR model types and `System.Text.Json` for serialization.
- `FhirGenerationPipeline` is the recommended entry point for any non-trivial dataset;
  `FhirBundleGenerator.Generate()` is suitable only for small/test datasets.
- `UploadBundlesSequentiallyAsync` aborts on first failure to preserve resource dependency
  ordering guarantees.
- Pipeline-derived resources (`Patient`, `MeasureReport`, `OperationOutcome`) are predicted
  deterministically at the count level so strict prediction-vs-actual reconciliation stays
  sound.
