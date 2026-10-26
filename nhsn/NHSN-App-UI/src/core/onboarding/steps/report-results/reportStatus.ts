import type { ReportPatientEntry, ReportStatus } from '../../../api/contracts';

// nhsn-react-core's Badge defaults to a small `shape="circle"` icon-count indicator and is
// `aria-hidden` - it's the wrong shape for a readable status label, so status renders as its own
// soft pill instead (pale background, bold saturated text), matching the onboarding POC.
export const STATUS_PILL_CLASS: Record<ReportStatus, string> = {
  Complete: 'nhsn-link__status-pill--complete',
  Pending: 'nhsn-link__status-pill--pending',
  Failed: 'nhsn-link__status-pill--failed',
  Cancelled: 'nhsn-link__status-pill--cancelled',
};

// Reuses the same pill classes for the per-patient report status column. Link's ReportingStatus has
// no "Critical Failure" value -- PatientIdentified/PendingValidation (validation hasn't run yet)
// share the pending pill instead of inventing a category the platform never produces.
export const STATUS_PILL_CLASS_BY_KEY: Record<string, string> = {
  notEligible: 'nhsn-link__status-pill--pending',
  pendingValidation: 'nhsn-link__status-pill--pending',
  failedValidation: 'nhsn-link__status-pill--failed',
  passedValidation: 'nhsn-link__status-pill--complete',
};

export interface ReportStatusSlice {
  labelKey: string;
  color: string;
  count: number;
  percent: number;
}

const STATUS_CATEGORY_COLOR: Record<string, string> = {
  notEligible: '#b45309',
  failedValidation: '#dc2626',
  pendingValidation: '#0b5cab',
  passedValidation: '#15803d',
};

// Link's ReportingStatus enum, folded into the four categories the report status pie shows.
// PatientIdentified/PendingValidation both mean "validation hasn't produced a verdict yet".
export function toStatusCategory(
  status: ReportPatientEntry['reportingStatus'],
): string {
  switch (status) {
    case 'NotReportable':
      return 'notEligible';
    case 'FailedValidation':
      return 'failedValidation';
    case 'PassedValidation':
      return 'passedValidation';
    case 'PatientIdentified':
    case 'PendingValidation':
    default:
      return 'pendingValidation';
  }
}

export function buildReportStatusBreakdown(
  patients: ReportPatientEntry[],
): ReportStatusSlice[] {
  if (patients.length === 0) {
    return [];
  }

  const counts = new Map<string, number>();
  patients.forEach((patient) => {
    const key = toStatusCategory(patient.reportingStatus);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  });

  return Array.from(counts.entries()).map(([labelKey, count]) => ({
    labelKey,
    color: STATUS_CATEGORY_COLOR[labelKey],
    count,
    percent: Math.round((count / patients.length) * 100),
  }));
}
