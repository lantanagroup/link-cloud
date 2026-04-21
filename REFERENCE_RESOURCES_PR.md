# LNK-XXXX: Restore batched FHIR Search for reference resources and decouple from the discoverer

### 🛠️ Description of Changes

Restores the batched `Search?_id=a,b,c` / `SearchPost` mechanics for reference-resource acquisition that commit `8b40855b` replaced with inline per-id `Read` calls, **without** reintroducing the shared-`FhirQuery`-row write contention that `8b40855b` was fixing. Reference resources are now acquired by real referential-phase `DataAcquisitionLog` rows, produced by a new transactional promoter that drains a correlation-scoped staging table after the Initial phase is terminal.

Net effect for a correlation that discovers N reference ids across K resource types with `Paged = P`:

| | FHIR round-trips | Executed on | Honors `OperationType`/`Paged` |
|---|---|---|---|
| Pre-`8b40855b` | `Σ ⌈Nₖ/P⌉` batched | reference-log thread | ✅ / ✅ |
| `8b40855b` (current `dev`) | up to N (per-id `Read`) | **primary-log thread** | ❌ / ❌ |
| **This PR** | `Σ ⌈Nₖ/P⌉` batched | referential-log thread | ✅ / ✅ |

---

### High-level architecture

```
Primary log processes bundle
   └─ ReferenceResourceService.ProcessReferences
        └─ ReferenceResourcesManager.StagePendingReferencesAsync
             INSERT INTO PendingReferenceIds (Facility, Correlation, Type, Id)
             — no FHIR, no cache, no Kafka. Unique index is the dedupe.

Primary log reaches terminal status

AcquisitionProcessingJob tick
   └─ PromoteReadyReferentialPhases  (NEW housekeeping step)
        └─ ReferentialPhasePromoter.FindAndPromoteReadyCorrelationsAsync
             Per correlation whose Initial is terminal AND no Referential log yet:
               - Resolve ReferenceQueryConfig per type from the query plan
               - Create one DataAcquisitionLog per type, transactional:
                   FhirQuery.IsReference = true
                   QueryType = Search | SearchPost   (from OperationType)
                   Paged = config.Paged
                   IdQueryParameterValues = batched ids
               - DELETE consumed PendingReferenceIds rows

AcquisitionProcessingJob same/next tick
   └─ ProcessPendingLogs picks up the new referential logs
        └─ FhirApiService.ExecuteSearch (IsReference branch — unchanged from pre-8b40855b)
             Batched GET /Type?_id=... or POST /Type/_search per Paged
             └─ PersistAcquiredReferenceResourcesAsync   (NEW)
                   Upserts canonical ReferenceResources cache
                   Junctions rows to THIS referential log
             └─ Kafka ResourceAcquired (unchanged)
             └─ ResourceIds appended to log
```

---

### Changes

**Schema**
- New entity `PendingReferenceId` (reshaped): natural key `(FacilityId, CorrelationId, ResourceType, ResourceId)` with unique index; secondary index on `(Facility, Correlation)` for promoter sweeps. FK to `FhirQuery` removed — the staging table is decoupled from any log.
- New migration `20260506000000_AddPendingReferenceIds`.

**Discovery (hot path — must not block primary-log processing)**
- `ReferenceResourceService.ProcessReferences` gutted to a single call: `IReferenceResourcesManager.StagePendingReferencesAsync`.
- New `StagePendingReferencesAsync` on the manager: chunked, conflict-tolerant upsert. Race-window on the unique index is caught and swallowed; concurrent primaries in the same correlation do not contend.
- Constructor of `ReferenceResourceService` trimmed (`_readFhirCommand`, `_kafkaProducer`, `_dbContext` removed — all now irrelevant on this path).

**Promoter (new)**
- `IReferentialPhasePromoter` / `ReferentialPhasePromoter`:
  - `PromoteAsync(facilityId, correlationId, ct)` — per-correlation transactional drain with in-transaction idempotency check.
  - `FindAndPromoteReadyCorrelationsAsync(maxPerRun, ct)` — housekeeping driver gated on "all Initial-phase logs terminal + no Referential log exists."
- Wired into `AcquisitionProcessingJob.Execute` as `PromoteReadyReferentialPhases`, adjacent to the existing `FailStalledQueuedLogs` / `ResetStalledProcessingLogs` steps.
- New setting `AcquisitionWorker.Processor.MaxReferentialPromotionsPerRun` (default 50) bounds per-tick work.

**Execution (the pipeline finishes the job)**
- `FhirApiService.ExecutePagingSearch` updated:
  - **Skips chained-reference staging when the current log is itself a reference log** — references discovered inside reference bundles do not re-enter the promoter loop. Matches single-level historical behavior.
  - **Persists acquired resources to the canonical `ReferenceResources` cache and junctions them to the referential log** via the new `PersistAcquiredReferenceResourcesAsync`. Upserts are idempotent on `(Facility, Type, Id)`, making the cache authoritative for cross-correlation reuse.

**Query-side cleanup (remove synthesis that hid 8b40855b's data shape)**
- `DataAcquisitionLogQueries.SearchQueryLogSummaryAsync` — removed the `refResourceTypesByLogId` junction-walk merge. A log's resource types are now strictly its own `FhirQuery` types.
- `DataAcquisitionLogQueries.GetDataAcquisitionLogStatisticsByReportAsync` — removed the `completedReferenceResourceIds` dedup pass. Stats walk `l.ResourceIds` once across all completed logs; every acquired resource is counted exactly once.
- `QueryLogSummaryModel.IsReferenceLog` is now authoritative (derived from `FhirQuery.IsReference`), not a proxy for junction-row presence.

**Dead-code removal**
- Deleted `IReferenceResourcesManager.CreateAsync` (single-row). Zero callers; `CreateBatchAsync` serves all live paths.
- Deleted `IReferenceResourcesManager.UpdateAsync`. Zero callers.
- Deleted `IReferenceResourcesManager.UpdateBatchAsync`. Zero callers.
- Deleted `UpdateReferenceResourcesModel.cs`. Only referenced by the three dead methods above.
- Deleted `ReferenceQueryLookupResult.cs`. Leftover projection DTO from an intermediate reference-resource refactor with zero consumers.
- Updated stale `// references are resolved inline` comment in `QueryListProcessor.Process` to describe the staging/promoter flow.

**DI**
- `GeneralStartupExtensions` registers `IReferentialPhasePromoter → ReferentialPhasePromoter` (Transient).

---

### Files touched

```
Application/Services/ReferenceResourceService.cs            [modified]
Application/Services/ReferentialPhasePromoter.cs            [new]
Application/Services/FhirApi/FhirApiService.cs              [modified]
Application/Services/QueryListProcessor.cs                  [comment updated]
Application/Managers/ReferenceResourcesManager.cs           [modified]
Application/Queries/DataAcquisitionLogQueries.cs            [modified]
Application/Models/UpdateReferenceResourcesModel.cs         [deleted]
Application/Models/ReferenceQueryLookupResult.cs            [deleted]
Infrastructure/Entities/PendingReferenceId.cs               [reshaped]
Infrastructure/Context/DataAcquisitionDbContext.cs          [DbSet<PendingReferenceId>]
Migrations/20260506000000_AddPendingReferenceIds.cs         [new]
Migrations/20260506000000_AddPendingReferenceIds.Designer.cs[new]
Migrations/DataAcquisitionDbContextModelSnapshot.cs         [regenerated]
Extensions/GeneralStartupExtensions.cs                      [DI registration]
Settings/AcquisitionWorkerProcessorSettings.cs              [new MaxReferentialPromotionsPerRun]
DotNet/DataAcquisition/Jobs/AcquisitionProcessingJob.cs     [promoter wired]
```

### Behavior changes observable in UIs

| Surface | Before this PR (on `8b40855b`) | After this PR |
|---|---|---|
| Acquisition log list — `QueryPhase = Referential` filter | Returns zero rows for new correlations | Returns the referential logs the promoter produced |
| Primary-log detail drawer | Shows a "Reference Resources" list (junctioned off the primary) | Shows nothing under reference resources (correct: primary logs don't own references) |
| Referential-log detail drawer | Does not exist for new correlations | Shows the resources that the referential log acquired |
| Stats donut by `QueryPhase` | `Referential` slice is 0 | `Referential` slice reflects actual promoted logs |

Admin.UI (`Web/Admin.UI/`, Angular) and Automation.UI (`DotNet/Automation.UI/`, Razor) both pick up the new data shape **without code changes** — the existing phase filter already includes `Referential`, and the detail drawer already gates the reference-resources panel on `referenceResourceCount > 0` (which is now correctly 0 on primary logs and >0 on referential logs). UX polish (e.g., hinting on a primary-log detail that references live on the referential row for the correlation) is deliberately **out of scope** here and tracked separately.

### Operational notes

- **Rollout**: no feature flag required. `PendingReferenceIds` ships empty; primaries immediately stage; the housekeeping promoter drains new correlations on the next `AcquisitionProcessingJob` tick. Correlations already past primary phase at deploy time complete without referential logs (equivalent to `8b40855b` inline behavior, not a regression).
- **Failure modes**: per-correlation promotion failure is logged and retried on the next tick. The promoter driver catches per-correlation exceptions without short-circuiting the pass.
- **Stragglers**: if late staging rows arrive after a correlation has already been promoted, the promoter's in-transaction idempotency check purges them without creating a second referential log.
- **Bounding**: `MaxReferentialPromotionsPerRun` caps per-tick work when a backlog accumulates.

---

### 🧪 Testing Performed

- Full solution build (`dotnet build`) — green.
- Existing integration test suite — green (pre-existing Razor `.cshtml` errors in `Automation.UI/Views/Runs/Details.cshtml` are unrelated to this change).
- New integration tests exercised against the standard `DataAcquisitionIntegrationTestFixture` (Testcontainers-backed):
  - `ReferentialPhasePromoterTests.PromoteAsync_CreatesReferentialLogAndPurgesStagedRows` — single-type end-to-end, asserts `FhirQuery` shape (`IsReference`, `QueryType`, `Paged`, `IdQueryParameterValues`, `FhirQueryResourceTypes`) and staging-table purge.
  - `ReferentialPhasePromoterTests.PromoteAsync_CreatesOneLogPerResourceType` — multi-type + `SiblingCount` stamping + per-type `OperationType` mapping (Search vs SearchPost).
  - `ReferentialPhasePromoterTests.PromoteAsync_IsIdempotentWhenReferentialLogAlreadyExists` — second call after more rows are staged does not create a duplicate referential log; stragglers are purged.
  - `ReferentialPhasePromoterTests.FindAndPromoteReadyCorrelations_SkipsCorrelationsWithNonTerminalInitialLogs` — gating check.
  - `ReferentialPhasePromoterTests.FindAndPromoteReadyCorrelations_PromotesCorrelationsWithTerminalInitialLogs` — release of the gate.
  - `ReferentialPhasePromoterTests.PromoteAsync_DropsStagedIdsForResourceTypesMissingFromPlan` — unresolvable configs do not leak staging rows.
  - `ReferenceResourceServiceTests.ProcessReferences_StagesDiscoveredIdsIntoPendingReferenceIds` (updated for staging-only semantics) — asserts no FHIR, no junction rows, no Kafka side effects on the primary path.

### 🧑‍🔬 Unit Testing

- [x] I have written or updated unit tests to cover my changes
- Coverage:
  - `ReferentialPhasePromoter` — 5 integration scenarios (see above).
  - `ReferenceResourceService.ProcessReferences` — rewritten to assert staging-only behavior.
  - `FhirApiService.ExecutePagingSearch` — existing unit tests adjusted to the new constructor dependency shape; the `IsReference` batched-Search path (unchanged from pre-`8b40855b`) remains covered by existing tests.

### 📓 Documentation Updated

- Inline XML doc on `PendingReferenceId` describes the new correlation-scoped schema, the unique-index-as-dedup contract, and the promoter lifecycle.
- Inline XML doc on `IReferentialPhasePromoter` describes `PromoteAsync` (per-correlation) vs `FindAndPromoteReadyCorrelationsAsync` (housekeeping driver), including the terminal-Initial + no-Referential gate.
- `IReferenceResourcesManager.StagePendingReferencesAsync` documents the race-safe upsert semantics and makes explicit that the unique index is the authoritative dedupe.
- `ReferenceResourceService.ProcessReferences` XML comment explains the staging-only model and points readers at the promoter.
- Stale "references resolved inline" comment in `QueryListProcessor.Process` updated.
- No external docs changes required: query plan authoring contract (`ReferenceQueryConfig.OperationType` / `Paged` / `ResourceType`) is unchanged; this PR makes those knobs effective again.
