# Org-Location Mapping — As-Built

**Jira:** [LEGPROG-227 — Location OrgID Resolution](https://lantana.atlassian.net/browse/LEGPROG-227)
**Scope:** Data Acquisition (and the downstream effect on MeasureEval / Report)

## Why this exists

Health systems that share a single FHIR endpoint (e.g. URMC) return encounter data for **every**
facility in the system. NHSN requires each submission bundle to contain data for a **single NHSN
Org ID** (minimum-necessary, HIPAA privacy rule). Org-location mapping lets a tenant declare which
**Locations** belong to its reporting organization, and Data Acquisition uses that to keep
non-org patients out of the report:

- A patient is **reportable** only if at least one of their encounters occurs at an **org location**
  (directly, or via a `partOf` ancestor that is an org location).
- For a non-reportable patient, dependent acquisition is **preempted** and the non-org encounter is
  **stripped from the resource cache** before MeasureEval, so the patient evaluates to a
  non-reportable outcome and is excluded from the report.

## When this flow is active

Org-location logic activates when the facility has an **active** `OrganizationLocationConfiguration`
with at least one `OrganizationLocationCondition` (a FHIRPath that flags a `Location` as an org
location). This is exactly what `ILocationMappingService.IsConfigured` checks — the set of active
conditions for the facility, cached per facility (1h, invalidated on change). When there are no active
conditions the mapping hooks and reportability gate are no-ops and every patient is treated as
reportable.

The facility's query plan must still acquire `Encounter` and resolve `Location` references for there
to be encounters/locations to map.

> Note: the `FhirQueryConfiguration.EnableLocationResolutionMapping` property still exists on the
> entity (and CRUD models) but is **no longer used** to gate this flow — activation is driven solely
> by `IsConfigured` (an active org-location config with conditions).

---

## End-to-end flow

```mermaid
flowchart TD
    %% ---------- Configuration (prerequisite) ----------
    subgraph CFG["Configuration (admin, one-time / on change)"]
        A1["Admin sets org-location config + conditions<br/>(FHIRPath per condition)<br/>active config + conditions = IsConfigured"]
        A1 -->|write| T_CFG[("OrganizationLocationConfiguration")]
        A1 -->|write| T_CON[("OrganizationLocationCondition")]
        A1 --> RECalc["On save: invalidate conditions cache<br/>+ ReevaluateLocationMappingsAsync<br/>(re-score already-cached Locations)"]
        RECalc -->|update| T_MAP[("OrganizationLocationMapping")]
        RECalc -->|cascade| T_EM[("EncounterMapping.MappedToOrg")]
    end

    %% ---------- Trigger ----------
    subgraph TRIG["Report trigger"]
        B1["Ad-hoc: Tenant.FacilityController → GenerateReportRequested<br/>Scheduled: Census PatientEvent → QueryDispatch"]
        B1 --> B2["DataAcquisitionRequested (per patient)"]
    end
    CFG --> TRIG

    %% ---------- Acquisition (worker) ----------
    subgraph ACQ["DataAcquisition.AcquisitionWorker — per (facility, correlationId)"]
        C0["Scaffold query-plan logs:<br/>Patient(Read), Encounter(Param), Location(Reference), Condition/dependents"]
        C0 -->|insert rows| T_LOG[("DataAcquisitionLog")]

        C1["AcquisitionDependencyChecker.CheckDependenciesAsync"]
        C0 --> C1
        C1 --> Cdep{"Org-location active?<br/>Dependent Initial log?"}
        Cdep -->|yes| Cgate["Gate behind Encounter + Location,<br/>then reportability"]
        Cdep -->|no| Crun["Run normally"]

        %% Primary acquisitions
        P_ENC["Acquire Encounter (Search)<br/>FhirApiService"]
        P_LOC["Acquire Location (Reference,<br/>discovered from Encounter.location)"]
        Crun --> P_ENC
        Cgate --> P_ENC

        P_ENC --> F1["FilterResourcesByEncounterMappingAsync<br/>(drop resources tied to non-org encounters)"]
        F1 --> H_ENC["UpdateEncounterLocationMappingAsync"]
        H_ENC -->|insert| T_EM
        H_ENC -->|insert junction| T_EL[("EncounterLocation")]
        H_ENC -->|placeholder if location unseen| T_MAP
        P_ENC -->|cache bodies| RC[("Resource cache<br/>correlationId:Encounter")]
        P_ENC -->|acquired ids| T_RID[("DataAcquisitionLogResourceId")]

        P_LOC --> H_LOC["UpdateLocationMappingAsync → IsOrgLocationAsync<br/>(evaluate conditions via cache)"]
        H_LOC -->|set IsOrgLocation| T_MAP
        H_LOC --> PARENT["Resolve PartOf + SetParentForChildrenAsync<br/>(children inherit IsOrgLocation)"]
        PARENT -->|cascade RecomputeMappedToOrg| T_EM
        P_LOC -->|cache bodies| RC

        %% Reportability gate
        REP{"IsPatientReportableAsync:<br/>any EncounterMapping.MappedToOrg = true?"}
        H_LOC --> REP
        H_ENC --> REP
        REP -->|reportable| DEP_OK["Acquire dependent resources<br/>(Condition, Observation, ...)"]
        REP -->|NOT reportable| PRE["Preempt dependent logs:<br/>status NotReportable"]
        DEP_OK -->|update status| T_LOG
        PRE -->|update status| T_LOG
        DEP_OK -->|cache bodies| RC
    end
    TRIG --> ACQ

    %% ---------- Tail ----------
    subgraph TAIL["Tail (once all sibling logs terminal)"]
        TL1["TryCompleteTailAsync (distributed semaphore,<br/>claims TailSent)"]
        TL2["StripNonOrgEncountersFromCacheAsync<br/>(remove MappedToOrg=false encounters from cache)"]
        TL3["Produce ResourcesAcquired (CacheKeys = correlationId:type)"]
        TL1 --> TL2 --> TL3
        TL2 -->|delete + rewrite| RC
    end
    REP --> TAIL
    PRE --> TAIL

    %% ---------- Downstream ----------
    subgraph DOWN["Downstream"]
        N1["Normalization: rehydrate cache, apply ops,<br/>write back → ResourcesNormalized"]
        M1["MeasureEval: build bundle from cache, run CQL.<br/>Non-org patient → no qualifying encounter → not reportable"]
        R1["Report: per-patient EntryMeasureReport"]
        N1 --> M1 --> R1
        M1 -->|upsert| MONGO[("Mongo: Resource,<br/>PatientReportingEvaluationStatus")]
        R1 -->|reportingStatus| T_RPT[("link-report: ReportEntry,<br/>EntryMeasureReport")]
    end
    TAIL --> DOWN

    %% ---------- Color scheme ----------
    classDef store    fill:#e6e9ff,stroke:#3b4cca,stroke-width:1px,color:#1a1a4a;
    classDef decision fill:#fff3cd,stroke:#cc9a06,stroke-width:1px,color:#5c4700;
    classDef hook     fill:#d4f1f4,stroke:#1c7c8c,stroke-width:1px,color:#0b3a42;
    classDef ok       fill:#d8f5d8,stroke:#2e7d32,stroke-width:1px,color:#14401a;
    classDef bad      fill:#ffd9d9,stroke:#c62828,stroke-width:1px,color:#5a1414;
    classDef strip    fill:#ffe2c2,stroke:#e07b00,stroke-width:2px,color:#5a3500;

    class T_CFG,T_CON,T_MAP,T_EM,T_EL,T_LOG,T_RID,RC,MONGO,T_RPT store;
    class Cdep,REP decision;
    class F1,H_ENC,H_LOC,PARENT hook;
    class DEP_OK ok;
    class PRE bad;
    class TL2 strip;

    style CFG  fill:#f3effa,stroke:#7a5fb0,color:#000;
    style TRIG fill:#eef6ff,stroke:#4a78c0,color:#000;
    style ACQ  fill:#f4faf4,stroke:#5a9a5a,color:#000;
    style TAIL fill:#fff7ec,stroke:#c79a4a,color:#000;
    style DOWN fill:#f6f0f7,stroke:#9a5fa0,color:#000;
```

**Legend** — phase backgrounds: 🟪 config · 🟦 trigger · 🟩 acquisition · 🟧 tail · 🟪 downstream.
Node types: 🟨 amber diamond = decision · 🩵 teal = org-location mapping hook · 🟩 green = reportable
path · 🟥 red = preemption (non-reportable) · 🟧 orange = cache strip (the key exclusion step) ·
🔵 blue cylinder = persisted data / cache.

---

## Per-correlation acquisition + storage (sequence)

```mermaid
sequenceDiagram
    autonumber
    participant W as AcquisitionWorker
    participant FA as FhirApiService
    participant LMS as LocationMappingService
    participant DB as link-dataacquisition (SQL)
    participant RC as Resource cache (Redis/ABS)

    Note over W: Encounter (Search) log
    W->>FA: ExecuteSearch(Encounter)
    FA->>LMS: FilterResourcesByEncounterMappingAsync
    FA->>LMS: UpdateEncounterLocationMappingAsync(encounter)
    LMS->>DB: insert EncounterMapping (+ EncounterLocation)
    Note right of LMS: MappedToOrg derived from referenced<br/>OrganizationLocationMapping.IsOrgLocation<br/>(placeholder mapping if location not yet seen)
    FA->>RC: UpdateCorrelationCache(correlationId:Encounter)
    FA->>DB: record DataAcquisitionLogResourceId (Encounter/id)

    Note over W: Location (Reference) log — discovered from Encounter.location
    W->>FA: ExecuteSearch(Location, IsReference)
    FA->>LMS: UpdateLocationMappingAsync(location)
    LMS->>LMS: IsOrgLocationAsync (eval conditions, cached)
    LMS->>DB: upsert OrganizationLocationMapping (IsOrgLocation, PartOf)
    LMS->>DB: SetParentForChildrenAsync (children inherit IsOrgLocation)
    LMS->>DB: RecomputeMappedToOrgForLocationMappingAsync (cascade to EncounterMapping)
    FA->>RC: UpdateCorrelationCache(correlationId:Location)

    Note over W: Dependent log (Condition, ...)
    W->>LMS: IsPatientReportableAsync(facility, patient)
    alt patient reportable (one or more org encounters)
        W->>FA: acquire dependent resource
        FA->>RC: cache + DataAcquisitionLogResourceId
    else not reportable
        W->>DB: set dependent log status = NotReportable
    end

    Note over W: Tail (all siblings terminal)
    W->>DB: TryCompleteTailAsync (claim TailSent)
    W->>LMS: StripNonOrgEncountersFromCacheAsync(correlationId, patient)
    LMS->>RC: delete correlationId:Encounter, rewrite org-only (empty if all non-org)
    W-->>W: Produce ResourcesAcquired (to Normalization, then MeasureEval)
```

---

## Storage logic & tables affected

All Data Acquisition tables live in the **`link-dataacquisition`** SQL database.

| Table (entity / DbSet) | When written | Key columns | Role in the flow |
|---|---|---|---|
| `OrganizationLocationConfiguration` (`LocationConfigurations`) | Admin config create/update | `ConfigId`, `FacilityId`, `IsActive` | Marks a facility as org-location enabled. `IsConfigured` keys off an active config with conditions. |
| `OrganizationLocationCondition` (`LocationConditions`) | Admin config create/update | `ConditionId`, `ConfigId`, `FhirPath`, `Priority` | FHIRPath rules that flag a `Location` as an org location. Cached per facility (1h, invalidated on change). |
| `OrganizationLocationMapping` (`OrganizationLocationMappings`) | Location acquired, or encounter references an unseen location (placeholder), or config re-eval | `LocationMappingId`, `FacilityId`, `LocationId`, `IsOrgLocation`, `PartOfValue`, `PartOfId` | Per-location org membership. `IsOrgLocation` set by evaluating conditions; children inherit via `PartOf` (`SetParentForChildrenAsync`, OR-semantics — never demote). |
| `EncounterMapping` (`EncounterMappings`) | Encounter acquired; recomputed on location-mapping change | `FacilityId`, `PatientId`, `EncounterId`, `MappedToOrg` | Per-encounter org membership. `MappedToOrg = true` if **any** referenced location is an org location. Drives `IsPatientReportableAsync` and the cache strip. |
| `EncounterLocation` (`EncounterLocations`) | Encounter acquired | `EncounterLocationId`, `EncounterMappingId`, `OrganizationLocationMappingId` | Junction between an encounter mapping and the org-location mappings it references (used for `MappedToOrg` recompute). |
| `DataAcquisitionLog` (`DataAcquisitionLogs`) | Scaffold + every status transition | `Id`, `FacilityId`, `CorrelationId`, `QueryPhase`, `Status`, `SiblingCount`, `TailSent` | One row per query. Status flips to `NotReportable` when a dependent log is preempted; `TailSent` gates tail completion. |
| `DataAcquisitionLogResourceId` (`DataAcquisitionLogResourceIds`) | Each resource acquired | `DataAcquisitionLogId`, `ResourceId` (`Type/id`) | Acquired resource **ids only** (not bodies). The tail derives `CacheKeys` from the distinct resource types here. |
| `DataAcquisitionLogReferenceResource`, `DataAcquisitionLogNote` | Reference resolution / per-step notes | — | Reference-fetch bookkeeping and human-readable acquisition notes (e.g. "Filtered out N resources"). |
| `QueryPlan` (+ `FhirQueryConfiguration`) | Admin config | Encounter / Location / dependent queries | Defines the queries that produce encounters and resolve `Location` references for mapping. Activation of org-location logic is driven by an active org-location config (`IsConfigured`), **not** by `EnableLocationResolutionMapping` (deprecated/unused). |

### Resource cache (not SQL)

- `IResourceCache` (Redis locally / ABS or hybrid in prod). **Resource bodies never travel on Kafka** —
  they are written to the cache keyed `correlationId:resourceType` (Redis hash, fields `TypeName/id`).
- The tail's `ResourcesAcquired` carries **cache keys**, not bodies. Normalization rehydrates each key.
- `StripNonOrgEncountersFromCacheAsync` mutates `correlationId:Encounter` **before** the tail is
  produced: it `Delete`s the key then rewrites only the org encounters (the cache write is an additive
  `HashSet`, so removal requires delete-then-rewrite). All-non-org → the key is left empty.

### Downstream stores

- **`link-measureeval` (Mongo):** `Resource`, `PatientReportingEvaluationStatus` (per facility+correlation,
  with a `reportable` flag per measure), `measureDefinition`. A stripped (all-non-org) patient yields no
  qualifying encounter → `reportable: false`.
- **`link-report` (SQL):** `ReportEntry` / `EntryMeasureReport`. The excluded patient lands at
  `reportingStatus = NotReportable` with no aggregate report blob.

---

## Reportability decision (summary)

```mermaid
flowchart LR
    S["Patient acquired"] --> Q1{"Org-location<br/>active for facility?"}
    Q1 -->|no| RYES["Reportable<br/>(acquire everything)"]
    Q1 -->|yes| Q2{"Any encounter<br/>mapping recorded?"}
    Q2 -->|no, fail-open| RYES
    Q2 -->|yes| Q3{"Any EncounterMapping<br/>MappedToOrg = true?"}
    Q3 -->|yes| RYES
    Q3 -->|no| RNO["Not reportable:<br/>preempt dependents (NotReportable)<br/>+ strip non-org encounters from cache<br/>→ MeasureEval non-reportable → excluded"]
```

## Key code references

- `Application/Services/LocationMappingService.cs` — `IsConfigured`, `UpdateLocationMappingAsync`,
  `UpdateEncounterLocationMappingAsync`, `IsOrgLocationAsync`, `IsPatientReportableAsync`,
  `FilterResourcesByEncounterMappingAsync`, `StripNonOrgEncountersFromCacheAsync`,
  `ReevaluateLocationMappingsAsync`.
- `Application/Services/AcquisitionDependencyChecker.cs` — org-location gating + reportability gate.
- `Application/Services/FhirApi/FhirApiService.cs` — acquisition loop + mapping hooks (`IsConfigured` gate).
- `Application/Managers/OrganizationLocationMappingManager.cs` — `SetParentForChildrenAsync`,
  `RecomputeMappedToOrgForLocationMappingAsync` (PartOf inheritance + cascade).
- `Application/Managers/OrganizationLocationConfigurationManager.cs` — cache invalidation + re-eval on save.
- `Application/Managers/DataAcquisitionLogManager.cs` — `TryCompleteTailAsync` (tail completion).
- `../DataAcquisition.AcquisitionWorker/Services/AcquisitionProcessorBackgroundService.cs` —
  preemption (`NotReportable`) and the cache strip before the tail produces `ResourcesAcquired`.

> Validation: `Tests/Postman/LEGLINK-142-NonReportable.postman_collection.json` drives this whole flow
> end-to-end against the local stack and asserts both the DataAcquisition mappings/status and the
> report-level exclusion.
