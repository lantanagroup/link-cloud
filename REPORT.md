
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
