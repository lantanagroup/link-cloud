import type {FacilityDraft} from '../../types';

/**
 * True once at least one measure exists that both has a resolved dQM and MeasureEval has a
 * definition for it -- not merely "a plan row exists". ReportingPlanStep stores what it found on
 * fetch (`hasAvailableMeasure`) so this can read the draft alone, matching every other step's
 * `isComplete` signature. Independent of the step's own displayed schedule, which is static.
 */
export function isReportingPlanComplete(draft: FacilityDraft): boolean {
  return Boolean(draft.reportingPlan.hasAvailableMeasure);
}
