import type {FacilityDraft} from '../../types';

export interface FieldErrors {
  [field: string]: string; // i18n keys, not sentences
}

export {PATIENT_ID_LIMIT} from './patientIds';

/**
 * The patient ids that count. Manual entry keeps a blank row while one is being
 * typed, so a bare length check would treat an untouched row as a patient.
 */
export function enteredPatientIds(draft: FacilityDraft): string[] {
  return (draft.report.patientIds ?? []).map(id => id.trim()).filter(Boolean);
}

export function validateReport(draft: FacilityDraft): FieldErrors {
  const errors: FieldErrors = {};
  const report = draft.report;

  if (!report.measures?.length) {
    errors.measures = 'onboarding:report.errors.measuresRequired';
  }

  if (!report.startDate) {
    errors.startDate = 'onboarding:report.errors.startDateRequired';
  }

  if (!report.endDate) {
    errors.endDate = 'onboarding:report.errors.endDateRequired';
  }

  if (enteredPatientIds(draft).length === 0) {
    errors.patientIds = 'onboarding:report.errors.patientIdsRequired';
  }

  // Both are ISO date-only strings, so a lexical comparison is a date
  // comparison and does not need parsing.
  if (report.startDate && report.endDate && report.endDate < report.startDate) {
    errors.endDate = 'onboarding:report.errors.endBeforeStart';
  }

  return errors;
}

/**
 * The step is done once a report has actually been requested. The parameters
 * being filled in is not enough — nothing has reached Link until the request
 * is made, and the Report Results step has nothing to show.
 */
export function isReportComplete(draft: FacilityDraft): boolean {
  return Boolean(draft.report.lastRequestedReportId);
}
