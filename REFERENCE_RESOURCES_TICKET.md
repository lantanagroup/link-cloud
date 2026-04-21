# LNK-XXXX: Restore batched FHIR Search for reference resources and decouple from the discoverer

**Issue type:** Story (Tech Debt / Regression)
**Components:** Data Acquisition, Acquisition Worker
**Labels:** `reference-resources`, `performance`, `regression`, `fhir`
**Priority:** High
**Fix versions:** _(next release)_

---

## Summary

The reference-resource acquisition path regressed during the LNK-5059 Data Acquisition Performance Update (commit `8b40855b`). To eliminate a write-hot-row deadlock on the shared reference log's `FhirQuery.QueryParameters` column, that change replaced the batched `Search?_id=a,b,c` query loop with **per-id `Read` calls executed inline on the primary-phase acquisition thread**. The batching, the `ReferenceQueryConfig.OperationType` (Search vs SearchPost), the `Paged` size, and the primary/reference-log separation were all collateral damage.

This ticket restores the batched Search mechanics that existed before `8b40855b`, **without reintroducing the shared-row write contention** that LNK-5059 was fixing — by staging discovered ids in a correlation-scoped table and promoting them into a referential-phase log once the primary phase of the correlation is terminal.

## Background

Three generations of the reference-resource flow existed:

| Version | FHIR mechanics | DB mechanics |
|---|---|---|
| **Pre-`8b40855b`** | `GET /Type?_id=a,b,c,...` batched by `Paged`, honoring `OperationType` | Every primary log called `IFhirQueryManager.UpdateAsync` to merge ids onto **one shared** `FhirQuery` row per reference log → deadlocks/timeouts under high correlation concurrency |
| **`8b40855b` (current on `dev`)** | `GET /Type/{id}` per id, serially, inline on the primary thread; `OperationType` and `Paged` ignored | No shared-row writes (problem intentionally avoided by abandoning batching). Junction rows stored against the *primary* (discoverer) log, entangling the discoverer with the acquired reference resources |
| **Target** | `GET /Type?_id=a,b,c,...` batched by `Paged`, honoring `OperationType` (same as pre-`8b40855b`) | Correlation-scoped staging table with a unique index serving as the dedupe; one transactional promotion into a real referential-phase `DataAcquisitionLog` per resource type |

For a correlation that discovers 100 reference ids across 3 resource types with `Paged = 25`:

- Pre-`8b40855b`: ~12 FHIR round-trips, batched.
- `8b40855b`: up to ~100 round-trips, serial, all on the primary thread (≈8× more).
- Target: ~12 FHIR round-trips, batched, off the primary thread.

## Problem

1. **~8× more FHIR round-trips** for typical correlations with discovered references, all serialized on the primary-log thread, inflating primary-phase wall time.
2. **`ReferenceQueryConfig.OperationType` is ignored.** Query plans that specify `SearchPost` (required by servers with URL length limits or those that don't accept comma-delimited `_id`) are silently bypassed. Plan-driven behavior is broken.
3. **`Paged` is ignored.** Servers with max-param-count limits on `_id=` can fail a `Search` that pre-`8b40855b` would have chunked correctly.
4. **Discoverer ↔ correlation coupling.** `DataAcquisitionLogReferenceResource` junction rows are written against the *primary* log that discovered each reference. The UI layer had to synthesize reference types back onto primary logs to compensate (`DataAcquisitionLogQueries.SearchQueryLogSummaryAsync` merged `l.ReferenceResources` into the displayed type list), which is fragile and leaks the internal wiring into the read model.
5. **Reference logs vanished from the UI.** The `QueryPhase = Referential` filter in Admin.UI and Automation.UI returned zero rows for any correlation processed after `8b40855b`, because no referential logs exist in the inline-fetch design.
6. **Cross-correlation cache utility diluted.** `ReferenceResources` is still populated, but because junctions are per-discoverer, the cache's "who consumed this" relationship is effectively scrambled; it works as a dedup on the write side only.
7. **Dead code introduced by the transition.** Sproc/model/method remnants from the intermediate design (`IReferenceResourcesManager.CreateAsync/UpdateAsync/UpdateBatchAsync`, `UpdateReferenceResourcesModel`, `ReferenceQueryLookupResult`) are still in the tree without callers.

## Proposed solution

Break the primary-phase acquisition and the reference-resource acquisition into two transactionally separate stages connected by a staging table:

1. **Staging** (during primary-phase processing)
   Reshape `PendingReferenceIds` to be correlation-scoped with a natural key of `(FacilityId, CorrelationId, ResourceType, ResourceId)`. The unique index *is* the dedupe — no shared row, no `UpdateAsync`. Discovery inserts a row per `(type, id)` tuple and returns immediately. No FHIR call, no cache lookup, no Kafka publish on the primary's hot path.

2. **Promotion** (when a correlation's Initial phase is terminal)
   A `ReferentialPhasePromoter` service runs as a housekeeping step inside `AcquisitionProcessingJob`. It finds `(facility, correlation)` pairs whose Initial-phase logs are all terminal and whose staging table has rows, then, in a single transaction per correlation:
   - Looks up each `ReferenceQueryConfig` from the correlation's query plan.
   - Creates one referential `DataAcquisitionLog` per resource type, with `FhirQuery.IsReference = true`, `QueryType` mapped from `OperationType` (Search/SearchPost), `Paged` from the config, and `IdQueryParameterValues` = the batched ids.
   - Deletes the consumed staging rows.
   - Idempotent: if a referential log already exists for the correlation, any late stragglers are purged.

3. **Execution** (the normal pipeline)
   `AcquisitionProcessingJob` picks up the new referential logs on the next tick. `FhirApiService.ExecuteSearch`'s existing `IsReference` branch — which **already contains** the batched `_id` loop that existed pre-`8b40855b` — dispatches the queries honoring `OperationType` and `Paged`. Acquired resources are upserted into the canonical `ReferenceResources` cache and junctioned to the *referential* log (not the primary).

4. **UI cleanup**
   Remove the reference-type synthesis in `DataAcquisitionLogQueries.SearchQueryLogSummaryAsync` and the junction-walk in `GetDataAcquisitionLogStatisticsByReportAsync`. Primary logs' displayed types are their own FhirQuery types; referential logs carry the reference types themselves. Stats are counted once off `ResourceIds`.

5. **Dead-code removal**
   Delete orphaned manager methods and their models (`CreateAsync`, `UpdateAsync`, `UpdateBatchAsync`, `UpdateReferenceResourcesModel`, `ReferenceQueryLookupResult`).

## Acceptance criteria

- [ ] For a correlation discovering N reference ids across K resource types with `Paged = P`, the number of FHIR round-trips is `Σ ⌈Nₖ/P⌉` across `k ∈ K` — not N.
- [ ] `ReferenceQueryConfig.OperationType = SearchPost` in the query plan results in `POST /{Type}/_search` being used at execution; `Search` results in `GET /{Type}?_id=...`. Integration-testable.
- [ ] Primary-phase logs do not make FHIR calls, cache lookups, or Kafka publishes for reference resources. Discovery is staging-table inserts only.
- [ ] Referential-phase logs appear as real rows in `DataAcquisitionLogs` with `QueryPhase = Referential`. Admin.UI and Automation.UI's existing `Referential` filter surfaces them without UI code changes.
- [ ] Canonical `ReferenceResources` cache is populated by referential-log execution (not by primary-phase discovery). Upserts are idempotent on `(Facility, Type, Id)`.
- [ ] `DataAcquisitionLogReferenceResource` junction rows are linked to the **referential** log that acquired them, never to the primary log that discovered them.
- [ ] No shared-row write contention during primary-phase discovery. Load test: N parallel primaries in one correlation discovering the same ids produce zero EF deadlocks/timeouts.
- [ ] Promoter is idempotent. A second call for the same `(facility, correlation)` after a referential log already exists is a no-op and purges any stragglers.
- [ ] Promoter is bounded per tick (`AcquisitionWorker.Processor.MaxReferentialPromotionsPerRun`, default 50).
- [ ] `QueryLogSummaryModel.IsReferenceLog` is `true` iff the log has a `FhirQuery.IsReference = true`. Not a proxy for junction-row presence.
- [ ] `GetDataAcquisitionLogStatisticsByReportAsync` counts each acquired resource exactly once; no junction-walk dedup hack.
- [ ] Dead code removed: `IReferenceResourcesManager.CreateAsync/UpdateAsync/UpdateBatchAsync`, `UpdateReferenceResourcesModel.cs`, `ReferenceQueryLookupResult.cs`.
- [ ] Integration tests for `ReferentialPhasePromoter`: single-type promotion + staging purge; multi-type with `SiblingCount`; idempotency on repeat; gating on non-terminal Initial phase; drop of plan-unknown resource types.
- [ ] Integration tests for `ReferenceResourceService.ProcessReferences` assert staging-only semantics (no FHIR, no junction, no Kafka).

## Out of scope

- **Admin.UI (`Web/Admin.UI/`) polish.** Angular SPA continues to work on the new data shape — `Referential` phase filter returns rows, reference-resource detail drawer gates on `referenceResourceCount > 0` correctly. Opinionated UX improvements (primary-log detail hint, drop redundant `Query Phase` column in the reference sub-table, distinct phase pill for `Referential`) are separate tickets.
- **Automation.UI defensive synthesis.** The `IsReferenceLog || QueryPhase == "Referential" || ReferenceResourceCount > 0` OR fallback in `RunsController` is now redundant but harmless. Keep for defense-in-depth against downstream contract drift.
- **`ReferenceResourceService.FetchReferenceResources`** legacy facility-validation path. Contains a pre-existing no-op stub for missing references that predates both `8b40855b` and this ticket. Separate concern.
- **Dropping `ReferenceResources.QueryPhase` column.** Production writes now always stamp `Referential`, making the column carry redundant info. Existing rows retain meaningful values and the field is projected out on the Admin.UI API; the migration cost outweighs the win.
- **Schema change for staging table naming.** Column names mirror the natural key; no breaking changes to unrelated tables.

## References

- Regression commit: `8b40855b` — *LNK-5059: Data Acquisition Performance Update (#1546)*
- Pre-`8b40855b` reference-log execution: `FhirApiService.ExecuteSearch`, `IsReference=true` branch (unchanged in target — still the execution site)
- Query-plan contract: `ReferenceQueryConfig` (`OperationType`, `Paged`, `ResourceType`)
- Related entities: `PendingReferenceId`, `ReferenceResources`, `DataAcquisitionLogReferenceResource`, `DataAcquisitionLog`, `FhirQuery`

## Risk / rollout

- New migration `AddPendingReferenceIds` creates an empty table. No data migration for in-flight correlations — correlations already past primary phase at deploy time will complete without referential logs (equivalent to the `8b40855b` inline behavior, not a regression).
- Feature-flag rollout is **not** required: primaries immediately start staging; the housekeeping promoter drains new correlations on the next tick.
- The housekeeping promoter runs inside the existing Quartz `AcquisitionProcessingJob` tick; no new scheduler wiring.
- Monitoring: `ReferentialPhasePromoter` emits structured logs on every promotion and on every skip reason (non-terminal Initial, unresolvable config, seed-log missing).
