# Automation
# Automation

`Automation` is the platform-agnostic foundation library for synthetic FHIR generation, deterministic expectation modeling, and reusable automation helpers.

It intentionally has **no dependency on Link-specific services**. Instead, host projects such as `Automation.Link`, `Automation.UI`, and `BackendE2ETests` compose it with environment-specific orchestration.

---

## What this project is

At a high level, `Automation` provides the engine to:

1. **Generate realistic synthetic patient datasets** in FHIR R4 transaction bundles
2. **Control expected measure eligibility** per patient
3. **Model what pipeline output should look like** from the generated inputs
4. **Expose reusable monitoring/config/output primitives** that hosts can wire into end-to-end flows

Think of this project as the reusable "simulation + expectation" core.

---

## Project structure

```
Automation (this project - no Link dependencies)
|-- Generation/          - FHIR R4 bundle generation + expectation models
|-- Helpers/             - output abstractions, retry, monitoring, diagnostics
|-- Configuration/       - base config classes
`-- measures/            - embedded measure definition bundles (JSON)
```

---

## End-to-end conceptual workflow

Although orchestration happens in host projects, `Automation` is designed around this lifecycle:

1. **Input selection**
   - choose patient count / cohorts / profiles / measures / seed
2. **Deterministic generation**
   - produce clinically coherent FHIR resources and transaction bundles
3. **Manifest construction**
   - build concrete generated resource-key and resource-count maps
4. **Acquisition + CQL reachability modeling**
   - determine which generated resources are expected to be acquired and expected to appear in final artifacts
5. **Validation support**
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

- `Generate(id, seed, ...)` - deterministic seed-based selection
- `Create(id, callerValues, ...)` - explicit caller-controlled construction

Factories include:

`PatientFactory`, `EncounterFactory`, `ConditionFactory`, `MedicationRequestFactory`, `MedicationFactory`, `MedicationAdministrationFactory`, `ObservationFactory`, `ProcedureFactory`, `DiagnosticReportFactory`, `ServiceRequestFactory`, `SpecimenFactory`, `CoverageFactory`, `ImagingStudyFactory`, `ImmunizationFactory`, `AllergyIntoleranceFactory`, `CareTeamFactory`, `CarePlanFactory`, `DocumentReferenceFactory`, `ProvenanceFactory`, `DeviceFactory`, `LocationFactory`, `OrganizationFactory`, `PractitionerFactory`, `CensusListFactory`.

### Reference linkage model

Resources are linked during generation (not patched after the fact), for example:

- `MedicationRequest.medication` -> `Reference(Medication/{id})`
- `MedicationAdministration.request` -> `Reference(MedicationRequest/{id})`
- `ImagingStudy.basedOn` -> `Reference(ServiceRequest/{id})`
- `Provenance.target` -> `Reference(DiagnosticReport/{id})`

This yields a consistent patient graph that downstream processes can traverse deterministically.

### Clinical terminology source (`FhirGenerationCodes`)

Centralized code tables include SNOMED, ICD-10, RxNorm, LOINC, and CVX-backed selections for demographics, practitioners, scenario definitions, observations, medications, procedures, service requests, and related assets.

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

In practice:

`PatientCohortDefinition` -> `PatientProfile` -> `FhirBundleGenerator` output -> `GenerationManifest` expectations.

---

## Predictive expectation model

`GenerationManifest` is the project's core expectation object. It records what was generated and exposes helper logic for "what should appear downstream" calculations.

Notable manifest capabilities:

- stores per-patient `ResourceType/Id` keys and counts
- tracks selected measure IDs and patient eligibility
- supports acquired-type filtering and expected-in-artifact filtering
- supports deterministic key-level prediction integration (`SimulatedAcquiredResourceKeysByPatient`)

Related generation helpers:

- `QueryPlanDefaults`
  - canonical default query-plan definition and acquired-type extraction
- `QueryPlanAcquisitionSimulator`
  - deterministic replay of parameter/reference query semantics against generated bundles
- `CqlResourceTypeExtractor`
  - extracts CQL-retrieved resource types from measure bundles

Together, these allow host validators to compare actual pipeline artifacts against deterministic expectations derived from known inputs.

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
