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
  censusAcquisition: CensusAcquisition | undefined,
  /** The list id field the user is actively editing, if any -- see findDuplicatePatientListIdKeys. */
  editedListKey?: CensusListKey
): FieldErrors {
  const errors: FieldErrors = {};
  const c = draft.census;

  if (censusAcquisition === 'PatientList') {
    CENSUS_LIST_KEYS.forEach(key => {
      if (!c.patientListIds?.[key]?.trim()) {
        errors[`listId.${key}`] = 'onboarding:census.errors.listIdRequired';
      }
    });
    findDuplicatePatientListIdKeys(c.patientListIds, editedListKey).forEach(key => {
      errors[`listId.${key}`] = 'onboarding:census.errors.listIdDuplicate';
    });
  } else if (censusAcquisition === 'Sftp') {
    if (!c.sftpHost?.trim()) {
      errors.sftpHost = 'onboarding:census.errors.hostRequired';
    } else if (c.sftpHost.trim().length > 128) {
      errors.sftpHost = 'onboarding:census.errors.hostTooLong';
    } else if (!isValidSftpHost(c.sftpHost.trim())) {
      errors.sftpHost = 'onboarding:census.errors.hostInvalid';
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
    if (totalMinutes === undefined || totalMinutes < 5) {
      errors.acquisitionFrequency = 'onboarding:census.errors.frequencyTooShort';
    } else if (totalMinutes > 1440) {
      errors.acquisitionFrequency = 'onboarding:census.errors.frequencyTooLong';
    }
  }

  return errors;
}

/**
 * Flags every member of a duplicate-value group except one "kept" row. The row the user is
 * actively editing is the one that should carry the error -- not just whichever sits later in
 * the list -- so it's kept out of contention for the clean slot whenever it's part of the group.
 * With no active edit (initial load, a CSV/census import), the first occurrence in list order
 * stays clean, same as before.
 */
export function findDuplicatePatientListIdKeys(
  patientListIds: Partial<Record<CensusListKey, string>> | undefined,
  editedKey?: CensusListKey
): Set<CensusListKey> {
  const keysByValue = new Map<string, CensusListKey[]>();

  CENSUS_LIST_KEYS.forEach(key => {
    const value = patientListIds?.[key]?.trim();
    if (!value) {
      return;
    }
    const keys = keysByValue.get(value);
    if (keys) {
      keys.push(key);
    } else {
      keysByValue.set(value, [key]);
    }
  });

  const duplicates = new Set<CensusListKey>();
  keysByValue.forEach(keys => {
    if (keys.length < 2) {
      return;
    }
    const kept = keys.find(key => key !== editedKey) ?? keys[0];
    keys.forEach(key => {
      if (key !== kept) {
        duplicates.add(key);
      }
    });
  });

  return duplicates;
}

/**
 * Mirrors Data Acquisition's own SftpConfigurationValidationRules.BeValidHostName — sFTP connects
 * by bare hostname or IP, never a URL, so this catches a pasted `http://...` value client-side
 * instead of round-tripping to a 400.
 */
function isValidSftpHost(host: string): boolean {
  if (/\s|\/|\\/.test(host)) {
    return false;
  }
  return !(
    host.startsWith('.') ||
    host.startsWith('-') ||
    host.endsWith('.') ||
    host.endsWith('-')
  );
}
