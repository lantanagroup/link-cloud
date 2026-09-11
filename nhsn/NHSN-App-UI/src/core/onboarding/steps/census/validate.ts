import type {CensusAcquisition, CensusListKey} from '../../../api/contracts';
import {parseHoursMinutesDuration} from '../../../shared/duration';
import type {FacilityDraft} from '../../types';

export interface FieldErrors {
  [field: string]: string; // i18n keys, not sentences
}

/** Canonical order — matches the Epic panel and `VendorProfile.patientListKeys`. */
export const CENSUS_LIST_KEYS: readonly CensusListKey[] = [
  'admit-lt-24',
  'admit-24-to-48',
  'admit-gt-48',
  'discharge-lt-24',
  'discharge-24-to-48',
  'discharge-gt-48'
];

/**
 * `censusAcquisition` drives which branch is required, never a vendor name.
 * The component reads it from `vendorProfile`; callers without one (flow.ts's
 * `isComplete`, which only receives the draft) infer it from which config the
 * draft actually carries.
 */
export function validateCensus(
  draft: FacilityDraft,
  censusAcquisition: CensusAcquisition | undefined
): FieldErrors {
  const errors: FieldErrors = {};
  const c = draft.census;

  if (censusAcquisition === 'PatientList') {
    CENSUS_LIST_KEYS.forEach(key => {
      if (!c.patientListIds?.[key]?.trim()) {
        errors[`listId.${key}`] = 'onboarding:census.errors.listIdRequired';
      }
    });
  } else if (censusAcquisition === 'Sftp') {
    if (!c.sftpHost?.trim()) {
      errors.sftpHost = 'onboarding:census.errors.hostRequired';
    }
    if (c.sftpPort === undefined) {
      errors.sftpPort = 'onboarding:census.errors.portRequired';
    } else if (!Number.isInteger(c.sftpPort) || c.sftpPort < 1 || c.sftpPort > 65535) {
      errors.sftpPort = 'onboarding:census.errors.portInvalid';
    }
  }

  if (censusAcquisition) {
    const parsed = parseHoursMinutesDuration(c.acquisitionFrequency);
    const totalMinutes = parsed ? parsed.hours * 60 + parsed.minutes : undefined;
    if (totalMinutes === undefined || totalMinutes < 15) {
      errors.acquisitionFrequency = 'onboarding:census.errors.frequencyTooShort';
    }
  }

  return errors;
}
