
## Prediction & validation port audit (Oct 11, 2026)

Automation.UI's engine was moved byte-for-byte (git R100) into Link.UI/Engine on Oct 7 (e8968f2a4); Automation.UI now compiles those files by link. No prediction/validator logic changed since the move. Core prediction (AbsSubmissionPredictor, MeasureInitialPopulationResolver, OrgResourceMapPredictionFilter, GenerationManifest) and the 9 database/ABS validators were already in Automation / Automation.Link.

Fixed:
- Re-homed into Automation.Link: ValidatorRunner, NormalizationSuiteApplicationValidator (+ NormalizationRuntimeSequencePlanner, NormalizationExecutionSummaryParser, NormalizationEvidenceSnapshot, NormalizationModels, NormalizationSuiteResolution records), LiveExpectedStateTracker (+ LivePatientStateSnapshot, LivePatientPoolEntry, PatientStateEvent, PatientEventType). Namespaces kept, so callers did not change; HTTP status constants became the same ints.
- The old Manifest page's prediction-vs-ABS rules (re-implemented in Link.UI ReportManifestRules) moved to Automation/Generation/AbsPredictionComparison; Link.UI keeps paging, sorting and badge styling only.
- Bug (porting): on multi-measure runs, Link.UI credited every predicted patient to the first measure. It now counts and labels each measure separately (per-patient membership and stage drill-downs too), matching Automation.UI's per-patient qualifying measures.
- Bug (original): the "Filtered out" tile could go negative; it now floors at 0 (Link.UI already did this; now pinned).

Live (A): Adhoc 96e706ae and Multi Measure c56fe6b2: totals, 18/19 type verdicts and the eligibility counts match the original JS logic run on the same snapshots; all 8 validators passed. Shots: shots\prediction-port\.

24d4820a: pipeline bug, not porting. Patient-afb68d22-001 had 28 Observations acquired (log 5754, Completed), Normalization 190 resources / 0 failures, every other type for that patient matched prediction exactly, and the other 3 patients' Observations matched exactly; only that patient's Observations never reached the ABS artifact. Loss sits between normalization and Report aggregation.

Tests: Link.UI.Tests 681/681. ServiceTests 4,854 passed, 5 failed (pre-existing at ad4b9c300: Kafka key-format integration tests).

## Oct 11 late: Kafka key tests, Observation-loss root cause, resource-type bars and palette

- ServiceTests: 5 integration tests updated for the LEGLINK-1440 JSON composite key; the 3 Report listener tests also verified the old sync Produce while the producer now calls ProduceAsync. Producers match the key design. Vendor audits put the vendor id in FacilityId, so their key is facility-shaped (worth a look on dev). Full ServiceTests: 4,860 passed, 2 skipped, 0 failed.
- 24d4820a Observation loss, root cause (Data Acquisition, also on origin/dev since LEGLINK-1109 #1867): DataAcquisitionLogManager.UpdateAsync committed Status=Completed before the log's resource-id rows. A sibling log finishing at the same moment claimed the tail and built ResourcesAcquired.CacheKeys without that type; the late log's own tail check found the group already claimed. No error, and no "Dropped" log. Evidence: the patient's Observation search was the last of 6 supplemental searches to finish, Normalization logged 0 Observation resources for that patient, and the MeasureReport resourceCount has no Observation. Fix: Status is written last (ff40953e9), with a regression test that fails without the fix.
- Repro: before the fix, 2 Adhoc runs passed; of 2 Multi Patient runs, one passed and 5a76b431 failed the same way (Patient-e53de696-094, Observation expected 7, actual 0). After the fix: 3 Multi Patient runs, 1 Adhoc and 1 Multi Measure all passed.
- Resource types: the legend is now a bar list (slice colour, count, %, biggest first) on the manifest overview and in the patient modal. The palette is Automation.UI's chart colours without greys, in chart-palette.js, DonutColors and the graph clusters. "Other" is always #adb5bd. Shots: shots/donut-bars/.
- Open: on real reports the overview donut card is headed "Populations", holds only "Other", and its centre says "resources" (an existing ReportManifestRules chart-model issue).
