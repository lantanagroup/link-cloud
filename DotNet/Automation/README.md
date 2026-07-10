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
+-- Generation/          FHIR R4 bundle generation, prediction model, streaming pipeline,
|                        imported-patient ingestion + classification, IP-window resolver,
|                        CQL filter simulator + per-resource-type filter profiles,
|                        deterministic acquisition simulator
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
- **Run-tag scoped IDs** -- there is no caller-supplied patient ID prefix. Each run
  generates a short hex `RunTag` (8 chars from a GUID) that scopes every resource ID:
  - Patients: `Patient-{RunTag}-{ordinal:D3}` (for example, `Patient-a1b2c3d4-001`).
  - Shared infrastructure: `{RunTag}-Loc-Hospital`, `{RunTag}-Org-Hospital`,
    `{RunTag}-Pract-001`, `{RunTag}-Med-001`, etc.
  - Per-patient resources: `{patientId}-Enc-001`, `{patientId}-Condition-primary`, etc.
  This guarantees that concurrent runs against the same FHIR server cannot collide on
  shared-infrastructure or per-patient resource IDs.
- **Clinical-period bounded** -- when the caller supplies
  `clinicalPeriodStart` / `clinicalPeriodEnd`, every encounter window is derived inside
  the supplied period (see `DeriveInpatientEncounterWindow` /
  `DeriveOutpatientEncounterWindow`). Without this, the seed-only encounter scheme could
  spill encounter tails past the period end and downstream date filters would silently
  drop late-encounter resources, producing a phantom "actual < expected" mismatch.
- **Chunked output** -- bundles are split at 500 entries to stay within FHIR transaction
  limits.

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
7. **Processes imported patients alongside generated ones** -- after the generated cohort
   has been uploaded, each entry in `importedPatients` is materialized (either reused from
   `PreLoadedEntries`, fetched via `Patient/{id}/$everything`, or parsed from supplied
   bundle JSON), classified for measure eligibility, recorded in the manifest, and
   simulated through the same acquisition + CQL paths as generated patients. Imported
   patient IDs are appended to `MarkPreExistingPatient` when sourced by ID, so cleanup
   skips expunging them.

Inpatient-pattern shaping is applied in this pipeline for both scheduled and non-scheduled
workflows whenever the profile carries `ScheduledInpatientPattern` and a clinical period is
provided. This keeps generation and prediction aligned with scenario-authored timing intent.

The pipeline accepts an optional `runId` parameter; when omitted a fresh short GUID is
generated so concurrent invocations remain isolated. Provide a stable `runId` only when
reproducing a specific run for debugging.

`FhirGenerationPipeline.PipelineResult` contains ordered patient IDs (generated first,
imported appended), a fully populated `GenerationManifest` (keys, counts, profiles,
simulated acquisition results, CQL exclusions, pre-existing patient set), and the total
bundle upload count.

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

## 5. FHIR data loading and fetching (`FhirDataLoader`)

`FhirDataLoader` is a thin REST client over a FHIR server. It handles uploads, retries,
bearer / basic auth, expunge cleanup, and (for imported-patient runs) paginated read
operations.

### 5.1 Uploads

| Method | Behavior |
|---|---|
| `UploadBundlesParallelAsync` | Concurrent upload with bounded parallelism. Used when dependency order is irrelevant (e.g., independent patients in batch mode). |
| `UploadBundlesSequentiallyAsync` | Strict sequential upload. **Aborts on first failure** -- logs remaining bundles as skipped and returns `false`. Preserves resource dependency order (Patient -> Encounter -> Observations). |

Support methods:

- `PostBundleWithRetryAsync` -- retries failed POSTs with configurable delay.
- `TrackCreatedResources` -- parses FHIR transaction response to record created resource IDs
  for later expunge.
- `WaitForServerAsync` -- polls the FHIR server's `metadata` endpoint before starting uploads.
- `DeleteResourcesWithExpunge` -- DELETEs every tracked resource with `_expunge=true`.

The sequential upload abort behavior is intentional: if a Patient bundle fails, dependent
Encounter and Observation bundles would fail with referential integrity errors anyway.
Aborting early keeps the server in a clean state.

### 5.2 Imported-patient fetch (`FetchPatientEverythingAsync`)

Executes `Patient/{id}/$everything` against the configured FHIR server and follows
`Bundle.link[next]` pages until the server stops emitting one. Returns a single merged
FHIR `Bundle` JSON string (the first page's envelope, with the union of every page's
`entry` array, the original first-page `link` block stripped, and `total` reset to the
merged entry count).

Pagination is non-negotiable for real-world servers (Cerner, Epic, HAPI sandboxes default
to 20-100 entries per page). Without it, the simulator would only see the first page of
the patient's data and predict `expected=0` for every resource type that spilled past the
page boundary, while the production DA pipeline -- which queries the server directly per
resource type -- sees everything.

Defensive guardrails:

- Absolute `next` links are followed via a one-off `RestClient` so they can target a
  different host than the configured base.
- A safety cap of 1000 pages prevents an infinite loop if a server returns the same
  `next` link forever; exceeding it throws.
- Cancellation is honored between pages via the supplied `CancellationToken`.

---

## 6. Imported patients (`ImportedPatientInput`, `ImportedPatientLoader`, `ImportedPatientClassifier`)

Imported patients let a scenario include real FHIR data alongside the synthetic cohort.
Two source kinds are supported:

| Source | Where the data comes from | Uploaded to FHIR server during the run? | Expunged on cleanup? |
|---|---|---|---|
| `ExistingId` | Pre-fetched via `Patient/{id}/$everything` (paginated) | No -- assumed to already exist. | No -- the run never wrote them. |
| `Bundle` | Caller-supplied transaction bundle JSON | Yes -- treated like any uploaded patient. | Yes -- standard cleanup applies. |

### 6.1 `ImportedPatientInput`

Serializable scenario input describing one imported patient:

- `Source` -- `ExistingId` or `Bundle`.
- `PatientId` -- FHIR `Patient.id` (required for ID source; auto-filled from the bundle
  when omitted on bundle source).
- `FileName`, `BundleJson` -- bundle-source payload metadata and the raw FHIR JSON.
- `AutoDetect` -- when `true`, run the classifier to seed `MeasureEligibilities`. When
  `false`, the supplied map is honored verbatim (manual override).
- `MeasureEligibilities` -- the per-measure Q/NQ map driving manifest classification.
- `DetectedClinicalScenarioId` -- best-effort scenario detection result (informational).
- `PreLoadedEntries` -- transient, non-serialized cache populated by
  `ImportedPatientLoader` so the pipeline does not re-fetch / re-parse.

### 6.2 `ImportedPatientLoader`

Materializes `PreLoadedEntries` for every imported patient up front so:

- ID-based imports are fetched via `FhirDataLoader.FetchPatientEverythingAsync` exactly
  once per run.
- Bundle-based imports are parsed once via `ParseBundleEntries`, which accepts both
  transaction and searchset bundles, populates `Request.Url = "Type/id"` /
  `Request.Method = PUT` for entries that lack them, and asserts the bundle contains a
  `Patient` with the configured id.
- The runner can compute `ComputeEncounterDateRange(imports)` -- the union of all imported
  encounter `Period` values -- and widen the run's clinical period so imported encounters
  do not fall outside it. Without this expansion, an imported patient whose encounter
  sits outside the (otherwise-default) report period is silently classified non-qualifying
  by measure-eval even when the classifier reported them as qualifying.

### 6.3 `ImportedPatientClassifier`

Applies the same heuristics the generator uses, against an imported patient's resources:

- ACH Monthly / ACH Daily eligibility = patient has an inpatient encounter
  (`class.code` in `IMP`, `ACUTE`, `NONAC`).
- Hypoglycemic eligibility = patient qualifies for ACH AND has a `Condition` whose code
  matches one of the diabetic clinical scenarios (DKA E11.10 / SNOMED 420422005, or
  Diabetic Hypoglycemia E11.649 / SNOMED 421725003).
- `DetectedClinicalScenarioId` = best match against
  `FhirGenerationCodes.ClinicalScenarios` by primary diagnosis SNOMED or ICD code.

Results are advisory; UI surfaces them as pre-populated checkboxes the user can override
before saving the scenario.

------

## 7. Cohort and profile model

### 7.1 `PatientCohortDefinition`

Compact cohort inputs defining a group of patients:

- `PatientCount` -- how many patients to generate.
- `CohortQualification` -- explicit cohort intent (`Qualifying` / `NonQualifying`) used by
  prediction gating, independent of per-measure map drift.
- `MeasureEligibilities` -- per-measure `Qualifying` / `NonQualifying` map.
- `ScheduledInpatientPattern` -- encounter admit/discharge timing relative to the report
  period.
- `EligibleClinicalScenarioIds` -- which clinical scenarios to draw from (empty = all).
- `ResourcesPerPatientMin` / `ResourcesPerPatientMax` -- resource count range.

`ScheduledInpatientPattern` values:

- `AdmittedBeforePeriodRemainsInpatientAfterPeriod`
- `AdmittedBeforePeriodDischargedDuringPeriod`
- `AdmittedDuringPeriodRemainsInpatientAfterPeriod`
- `AdmittedDuringPeriodDischargedDuringPeriod`
- `AdmittedAndDischargedBeforePeriod`
- `AdmittedAndDischargedAfterPeriod`

### 7.2 `PatientProfile`

Expanded per-patient configuration produced by `PatientCohortDefinition.ExpandProfiles()`:

- Per-measure eligibility map.
- `CohortQualification` propagated from the source cohort.
- `ScheduledInpatientPattern` propagated from the source cohort.
- Seed offset for deterministic generation.
- Clinical scenario assignment (round-robin from eligible scenarios).
- Resource count (randomized within the cohort's min/max range).

`PatientProfile` exposes prediction helpers that combine measure eligibility with cohort-level
intent and pattern inclusion semantics, so hosts can compute expected submitted/ABS sets
without re-implementing rule logic.

### 7.3 Expansion flow

```
PatientCohortDefinition[]
    +-> ExpandProfiles(seed)
            +-> PatientProfile[]
                    +-> FhirBundleGenerator / FhirGenerationPipeline
                            +-> GenerationManifest (keys + predictions)
```

When `EligibleClinicalScenarioIds` is empty, expansion falls back to all clinical scenarios
from `FhirGenerationCodes.ClinicalScenarios`.

### 7.4 Inpatient pattern semantics in generation/prediction

`ScheduledInpatientPattern` influences two independent but coordinated behaviors:

1. **Encounter window derivation** -- controls admit/discharge placement relative to the
   report period (`before`, `during`, `after`).
2. **Prediction inclusion semantics** -- each pattern maps to `ExpectedInReport` via
   `ScheduledInpatientPatternExtensions.GetCensusBehavior()`.

Prediction helpers (`PatientProfile.IsExpectedInReportByCohortAndPattern()` and
`IsExpectedToBeSubmitted(...)`) combine:

- per-measure eligibility,
- `CohortQualification`,
- pattern `ExpectedInReport`.

This prevents expected-output inflation for cohorts intentionally configured as
non-reportable by timing or cohort qualification.

---

## 8. Predictive expectation model

`GenerationManifest` is the project's core expectation object. It records what was generated
and exposes helper logic for "what should appear downstream" calculations.

### 8.1 Building the manifest

Two construction modes:

1. **Batch** -- `GenerationManifest.Build(patientIds, profiles, bundles, ...)` processes retained
   bundle data after generation completes.
2. **Incremental** -- `GenerationManifest.IncrementalBuilder` accumulates entries per patient
   during streaming pipeline execution, then finalizes. Used by `FhirGenerationPipeline` so
   manifest metadata is captured from in-memory FHIR objects before they are serialized and
   discarded.

### 8.2 Manifest state

- Per-patient `ResourceType/Id` keys and counts.
- Selected measure IDs and per-patient eligibility.
- Query-plan acquired resource types (+ parameter-query subset).
- CQL-referenced resource types (from `CqlResourceTypeExtractor`).
- `SimulatedAcquiredResourceKeysByPatient` -- deterministic key-level acquisition replay.
- `CqlFilteredResourceKeysByPatient` -- per-resource SDE exclusions.
- Resource-level CQL context attributes used by the simulator, including reference/subject
  relationships for resources such as `Specimen` where a resource can be acquired by a
  reference query but still not be reachable from the measure's patient-context CQL retrieve.
- `ExpectedOperationOutcomeCountByPatient` -- post-hoc hook populated by
  `ReportAbsManifestValidator` from `ReportEntry.ReportingStatus`.
- `PreExistingPatientIds` -- patients that were already on the FHIR server before the run
  (imported by ID). Recorded via `IncrementalBuilder.MarkPreExistingPatient(patientId)`.
  Cleanup uses this set to skip expunging resources we did not upload.

### 8.3 Prediction formula

For each patient the predicted set of resources is computed in layers:

```
base        = simulated-acquired keys (fallback: generated keys) filtered by
              IsExpectedInAbs(resourceType)
base        = base minus CqlFilteredResourceKeysByPatient[patientId]
base        = empty when patient is excluded by cohort qualification/pattern inclusion
              semantics
base        = base plus Patient/{patientId}   when the patient qualifies for any measure
              (MeasureEval's CQL engine loads Patient implicitly)

derived +=  MeasureReport   : number of measures the patient qualifies for
derived +=  OperationOutcome: ExpectedOperationOutcomeCountByPatient[patientId]    (ABS only)
```

Final predicted resource-type counts = base counts + derived counts.

### 8.4 Two prediction destinations

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

### 8.5 CQL filter simulator semantics

`CqlFilterSimulator.ComputeFilteredKeys(measures, input)` returns resources that will be
excluded by SDE `where` semantics. The result is the **intersection** of exclusions across
applicable measure profiles **per resource type**, because MeasureEval evaluates each
measure independently and writes one `.mr` file per measure; `PatientAggregator` unions
contained resources across those files into the patient NDJSON. A resource is only truly
absent from ABS when every applicable measure for its resource type excludes it.
Profiles for other resource types do not participate in that intersection -- an
Observation profile has no opinion about whether a Condition belongs in ABS.

The simulator also models patient-context CQL retrieval. A DataAcquisition reference query
can acquire a resource because some other acquired resource references it, but the measure
CQL still evaluates `[ResourceType]` in the current patient context. Profiles therefore may
check patient ownership/reference fields before predicting inclusion. The most important
case today is `Specimen`: `Specimen.subject` must resolve to the evaluated patient before
monthly ACH or Hypoglycemic specimen rules can include it, and ACH Daily additionally
requires an included respiratory-pathogen observation to reference the specimen.

For multi-measure runs with per-patient eligibility, `FhirGenerationPipeline` passes only
each patient's **qualifying** measures into `ComputeFilteredKeys`, because a measure the
patient does not qualify for does not contribute contained resources to ABS and therefore
must not influence the intersection.

#### Initial-Population windows (`MeasureInitialPopulationResolver`)

Most SDE `where` clauses have the shape
`where exists "Initial Population" IP where Resource.X overlaps IP.period`. The simulator
resolves the patient's IP windows once per call by walking every encounter in the input
and keeping those whose `class.code` qualifies them for any selected measure:

- ACH Monthly + ACH Daily -- `class` in the NHSN inpatient set
  {`IMP`, `ACUTE`, `NONAC`, `SS`} plus direct emergency / observation codes
  {`EMER`, `OBSENC`}.
- Hypoglycemic -- `class` in the NHSN inpatient set {`IMP`, `ACUTE`, `NONAC`, `SS`}.

Known resolver gaps are documented in `EncounterIpClassification`: encounter type-based
qualification, encounter-location-based qualification, and the full Hypoglycemic
antidiabetic-medication requirement are approximated rather than fully CQL-evaluated.

The resulting `IpWindow` set is then passed to every applicable filter profile via
`PatientCqlInput.IpWindows`. The `IpWindowExtensions` helpers (`AnyOverlaps`,
`AnyContains`, `AnyEncounterMatches`, `AnyEndStrictlyAfter`, `AnyEndOnOrAfter`) let each
profile express its rule in terms of the resolved set rather than a single legacy
`EncounterStart`/`EncounterEnd` pair.

When a `PatientCqlInput` is built via the legacy positional constructor (no `Encounters`
list), the simulator falls back to a single window derived from
`EncounterId` + `EncounterStart` + `EncounterEnd` so pre-multi-encounter tests and
single-encounter generators continue to work unchanged.

#### Built-in profiles

`CqlFilterSimulator` ships with profiles spanning every resource type currently retrieved
by an SDE `define` in the supported measures:

| Profile | Resource type | Measures | Rule (summary) |
|---|---|---|---|
| `AchConditionFilterProfile` | `Condition` | ACH Monthly + Daily | `problem-list-item` requires `active` + `recordedDate` strictly before any IP `end`; OR `encounter-diagnosis` / `health-concern` referencing any IP encounter. |
| `HypoglycemicConditionFilterProfile` | `Condition` | Hypoglycemic | `recordedDate <= IP.end` (no active constraint). |
| `AchObservationFilterProfile` | `Observation` | ACH Monthly + Daily | `category` in {`laboratory`, `vital-signs`, `social-history`, `survey`, `imaging`, `procedure`} AND effective overlaps any IP. |
| `HypoglycemicObservationFilterProfile` | `Observation` | Hypoglycemic | LOINC in the blood-glucose lab/POC whitelist (2339-0, 2345-7, 41653-7) AND `start of effective during IP` (point-in-IP). |
| `AchProcedureFilterProfile` | `Procedure` | ACH Monthly + Daily | `performed` overlaps any IP. |
| `AchHypoMedicationRequestFilterProfile` | `MedicationRequest` | ACH + Hypoglycemic | `authoredOn` falls inside any IP. |
| `HypoMedicationAdministrationFilterProfile` | `MedicationAdministration` | Hypoglycemic | `effective` overlaps any IP. |
| `AchHypoCoverageFilterProfile` | `Coverage` | ACH + Hypoglycemic | `period` overlaps any IP (open ends supported). |
| `AchHypoServiceRequestFilterProfile` | `ServiceRequest` | ACH + Hypoglycemic | `authoredOn` falls inside any IP. |
| `AchEncounterFilterProfile` | `Encounter` | ACH Monthly + Daily + Hypoglycemic | Encounter must itself overlap an IP window (IP encounters trivially overlap themselves; ACH SDE also pulls in non-IP encounters that overlap IP). |
| `AchMonthlySpecimenFilterProfile` | `Specimen` | ACH Monthly | `Specimen.subject` resolves to the evaluated patient AND `collection.collected` overlaps any IP. This mirrors patient-context `[Specimen]` plus the monthly SDE `overlaps IP.period` predicate. |
| `AchDailySpecimenFilterProfile` | `Specimen` | ACH Daily | `Specimen.subject` resolves to the evaluated patient AND the specimen is referenced by a final/registered/preliminary/partial laboratory observation whose LOINC is in the respiratory pathogen (COVID-19, influenza, RSV) value sets used by the daily measure. Daily does **not** include every collected-in-period specimen. |
| `HypoglycemicSpecimenFilterProfile` | `Specimen` | Hypoglycemic | `Specimen.subject` resolves to the evaluated patient AND `collection.collected` is fully during any IP. |

All profiles are extracted from real generated FHIR content via
`CqlFilterInputExtractor` -- the simulator never replays seeds. Adding a new family is a
matter of implementing `ICqlFilterProfile` (which exposes `TargetResourceType`,
`AppliesToAny`, and `ComputeExcludedKeys`) and registering it in the `Profiles` list.

### 8.6 Related helpers

- `QueryPlanDefaults` -- canonical default query-plan definition and acquired-type extraction.
- `QueryPlanAcquisitionSimulator` -- deterministic replay of parameter / reference query
  semantics against generated bundles; simulates which resources DataAcquisition would
  acquire for each patient. The single-patient entrypoint
  `SimulateAcquiredKeysForPatient` accepts the patient's pre-parsed entries plus the
  shared infrastructure entries, the query plan, and an optional clinical period
  (`clinicalPeriodStart` / `clinicalPeriodEnd`), plus an optional
  `allowEncounterAnchoredDateOverrideForOutOfRange` mode switch:
  - When the query plan declares a `date=ge...` or `date=le...` parameter, the simulator
    extracts the candidate resource's date range (instant fields collapse to start == end)
    and applies FHIR overlap semantics: `ge S` requires `resource.End >= S`, `le E`
    requires `resource.Start <= E`.
  - **Fail-closed on unknown date shapes** -- when a date filter is in play but the
    candidate resource has no recognized date field, it is excluded from the predicted
    set and a one-time-per-resource warning is emitted via the optional `IAutomationOutput`
    sink. This keeps prediction honest for unfamiliar imported FHIR shapes; extending
    `TryGetResourceDateRange` is the way to model new shapes.
  - **Encounter-anchored out-of-range override (optional)** -- callers can opt-in to keep
    encounter-linked Observation/DiagnosticReport/Procedure resources when strict date-bound
    matching would otherwise exclude them. Hosts typically enable this for scheduled/regenerate
    workflows and keep strict mode for non-scheduled runs.
- `CqlResourceTypeExtractor` -- extracts CQL-retrieved resource types from measure bundles.
  Reachability roots include both population criteria expressions and `supplementalData`
  criteria expressions (SDE roots).
- `CqlFilterInputExtractor` -- walks generated entries to build the `PatientCqlInput` that
  the simulator consumes. Extracts:
  - All encounters (`EncounterContext` with class code, status, period start/end) so the
    IP-window resolver can pick which ones constitute the Initial Population per measure.
  - Conditions (`ConditionContext`).
  - Observations (`ObservationContext`).
  - Procedures (`ProcedureContext`).
  - MedicationRequests (`MedicationRequestContext`).
  - MedicationAdministrations (`MedicationAdministrationContext`).
  - Coverages (`CoverageContext`).
  - ServiceRequests (`ServiceRequestContext`).
  - Specimens (`SpecimenContext`, including `subject` and `collection.collected`).
  Observation extraction also captures `status` and `specimen` references for measure rules
  that include related specimens through qualifying observations.
  Legacy single-encounter shape is preserved (`EncounterId` / `EncounterStart` /
  `EncounterEnd` pick the first encounter with a populated `Period`) so existing callers
  and tests continue to compile and behave the same.

Together, these allow host validators to compare actual pipeline artifacts against
deterministic expectations derived from known inputs.

---

## 9. Helpers (`Helpers/`)

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

## 10. Configuration (`Configuration/`)

- `AutomationConfigBase` -- common runtime settings (FHIR endpoints, cleanup toggles, auth
  modes).
- `TestScenarioConfigBase` -- scenario execution settings (measure inputs, patient IDs, polling
  windows, timeouts).
- `OAuthConfig` -- OAuth2 client-credentials configuration.
- `BasicAuthConfig` -- basic-auth credentials configuration.

Host projects extend these base classes for environment- or service-specific needs.

---

## 11. How `Automation` fits into the wider ecosystem

`Automation` is consumed by:

- **`Automation.Link`** -- adds Link service clients, facility/report orchestration, pipeline
  validators, and monitoring probes.
- **`Automation.UI`** -- interactive Razor MVC host for running and monitoring scenarios.
- **`BackendE2ETests`** -- automated test-host composition.

This layering keeps generation and expectation logic reusable and testable without
infrastructure coupling.

---

## 12. Notes

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
- There is no caller-supplied patient ID prefix. Every generated resource ID is scoped by
  the run's `RunTag` so concurrent runs against a shared FHIR server cannot collide. The
  legacy `patientIdPrefix` parameter has been removed from the generator, the pipeline,
  and the per-resource factories (for example, `CensusListFactory.Generate` now takes
  `(id, patientId, date)`).
- When generating against a configured Report Period, always pass the period through to
  the generator (`clinicalPeriodStart` / `clinicalPeriodEnd`) so encounter windows are
  bounded inside the period. Otherwise downstream date filters can drop late-encounter
  resources and produce a phantom "actual < expected" mismatch.
- Imported patients are first-class scenario inputs. `ImportedPatientLoader` pre-fetches
  / parses them once per run, the pipeline reuses the cached entries, and
  `GenerationManifest.PreExistingPatientIds` records ID-imported patients so cleanup
  skips expunging data the run did not upload.
