# Automation

`Automation` is the platform-agnostic foundation library for FHIR data generation, automation helpers, and base configuration. It has **no dependency on Link-specific services** — it provides the building blocks that `Automation.Link` and `BackendE2ETests` compose.

## Architecture

```
Automation (this project — no Link dependencies)
├── Generation/          — FHIR R4 bundle generation
├── Helpers/             — output abstractions, retry, monitoring, diagnostics
├── Configuration/       — base config classes
└── measures/            — embedded measure definition bundles (JSON)
```

## FHIR data generation (`Generation/`)

### Bundle generator

`FhirBundleGenerator` orchestrates deterministic synthetic FHIR R4 transaction bundle generation.

- **Scenario-driven** — each patient is assigned one of 16 clinical scenarios (pneumonia, MI, DKA, GI bleed, sepsis, PE, appendicitis, etc.) that drives clinically coherent resource selection.
- **Seed-deterministic** — same inputs (seed, patient count, resources per patient) always produce identical output.
- **Measure-aware** — `GenerateWithProfiles()` accepts `PatientProfile` lists with `MeasureEligibility` (Qualifying/NonQualifying) to generate patients that pass or fail specific measures.
- **Multi-measure** — qualifying patients can satisfy multiple measures simultaneously (e.g., ACH Monthly + Hypo).
- **Chunked output** — bundles are split at 500 entries to respect FHIR server transaction limits.

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

### Scenario resource mapping (`ScenarioResourceMap`)

Each scenario maps to clinically appropriate subsets of the global resource pools:

- **Medications** — e.g., pneumonia → ceftriaxone/amoxicillin; DKA → insulin/potassium/NS
- **Procedures** — e.g., MI → CABG; appendicitis → appendectomy
- **Observations** — e.g., pancreatitis → lipase/bilirubin/calcium; PE → troponin/INR
- **Specimens** — e.g., COPD → sputum/ABG; cellulitis → wound swab
- **Imaging** — e.g., stroke → CT/MRI head; PE → CTA chest
- **Service requests** — scenario-appropriate lab panels and consults
- **Comorbidities** — scenario-appropriate secondary diagnoses

Universal inpatient resources (acetaminophen PRN, DVT prophylaxis, vitals, CBC, BMP) are included in every scenario.

### Resource factories (`ResourceFactories/`)

Each FHIR resource type has a factory with two methods:

- `Generate(id, seed, ...)` — seed-driven, picks from code pools using the seed index.
- `Create(id, callerValues, ...)` — fully caller-supplied values for explicit control.

Factories: `PatientFactory`, `EncounterFactory`, `ConditionFactory`, `MedicationRequestFactory`, `MedicationFactory`, `MedicationAdministrationFactory`, `ObservationFactory`, `ProcedureFactory`, `DiagnosticReportFactory`, `ServiceRequestFactory`, `SpecimenFactory`, `CoverageFactory`, `ImagingStudyFactory`, `ImmunizationFactory`, `AllergyIntoleranceFactory`, `CareTeamFactory`, `CarePlanFactory`, `DocumentReferenceFactory`, `ProvenanceFactory`, `DeviceFactory`, `LocationFactory`, `OrganizationFactory`, `PractitionerFactory`, `CensusListFactory`.

### Reference linkage

Resources are wired with FHIR references during generation (not post-hoc):

- `MedicationRequest.medication` → `Reference(Medication/{id})`
- `MedicationAdministration.request` → `Reference(MedicationRequest/{id})`
- `ImagingStudy.basedOn` → `Reference(ServiceRequest/{id})`
- `Provenance.target` → `Reference(DiagnosticReport/{id})`

### Code tables (`FhirGenerationCodes`)

Centralized clinical code tables with real SNOMED, ICD-10, RxNorm, LOINC, and CVX codes:

- 8 patient demographics, 6 practitioners, 16 clinical scenarios
- 25+ medications, 20+ procedures, 35+ observations, 7 specimen types
- 11 imaging studies, 8 diagnostic report panels, 7 document types
- 17+ service requests, 8 allergies, 10 immunizations, 10 care plan activities
- 24 comorbidity conditions

### Measure profiles

- `ProfiledMeasureType` — enum of supported measures (ACH Monthly, ACH Daily, Hypo).
- `ProfiledMeasureCatalog` — maps measures to display names and embedded bundle resource paths.
- `PatientProfile` — per-patient eligibility control (`Qualifying` / `NonQualifying`).
- `MeasureEligibility` — enum for qualifying vs. non-qualifying generation.

## Helpers (`Helpers/`)

- `IAutomationOutput` — platform-agnostic output abstraction (console, test output, event streams).
- `DualOutputHelper` — writes to both console and captured output.
- `TimestampedAutomationOutput` — adds timestamps to output lines.
- `RetryHelper` — generic async retry with configurable backoff.
- `StatusPollingHelper` — polls a status endpoint until a condition is met.
- `BackgroundMonitorLoop` — generic background polling loop with event emission.
- `MilestoneTracker` — tracks named milestones with timestamps.
- `ProgressTracker` — tracks per-item progress through pipeline stages.
- `DatabaseConnectionFactory` — base class for database connection resolution.
- `DiagnosticSnapshotWriter` — writes diagnostic snapshots to disk.
- `ValidationRunner` — coordinates validation execution.

## Configuration (`Configuration/`)

- `AutomationConfigBase` — base configuration (FHIR URLs, cleanup flags, auth settings).
- `TestScenarioConfigBase` — base scenario configuration (measure bundles, timeouts, patient IDs).
- `OAuthConfig` — OAuth2 client credentials settings.
- `BasicAuthConfig` — basic authentication settings.

## Notes

- Targets `.NET 8`.
- No dependency on Link-specific projects (`Shared`, `LinkSdk`, etc.).
- Uses `Hl7.Fhir.R4` for FHIR POCO types and `System.Text.Json` for serialization.
