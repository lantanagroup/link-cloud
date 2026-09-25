import type {FacilityDraft} from '../../types';

export interface FieldErrors {
  [field: string]: string; // i18n keys, not sentences
}

export interface FhirFieldValues {
  fhirServerBaseUrl: string;
  maxConcurrentRequests?: number;
  maxRetries?: number;
  minAcquisitionPullTime: string;
  maxAcquisitionPullTime: string;
  lagDays?: number;
  lagHours?: number;
  lagMinutes?: number;
}

function hasRealDomain(hostname: string): boolean {
  const normalized = hostname.endsWith('.') ? hostname.slice(0, -1) : hostname;
  const labels = normalized.split('.');
  return labels.length >= 2 && labels.every(label => label.length > 0);
}

// RFC 3986 requires percent-encoding for these characters; .NET's Uri.IsWellFormedUriString -
// what Data Acquisition validates the saved value against server-side - rejects any of them
// appearing unescaped, even though the URL constructor below happily accepts and re-encodes them.
const UNSAFE_URL_CHARACTERS = /[\x00-\x1F\x7F <>"{}|\\^`[\]]/;

export function isValidHttpUrl(value: string): boolean {
  if (UNSAFE_URL_CHARACTERS.test(value)) {
    return false;
  }
  try {
    const url = new URL(value);
    return (url.protocol === 'http:' || url.protocol === 'https:') &&
      (url.hostname === 'localhost' || hasRealDomain(url.hostname));
  } catch {
    return false;
  }
}

/** 24-hour HH:MM, hours 00-23, minutes 00-59 — what `normalizePullTime` in FhirStep produces on blur. */
const PULL_TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

const MAX_CONCURRENT_REQUESTS_CAP = 8;

// Mirrors the BFF's FieldValidationRules.LagDurationCapMinutes (59 days), shared with the
// manual-upload import path - keep the two in sync, they've drifted apart before.
const LAG_DAYS_CAP = 59;

function toMinutesSinceMidnight(pullTime: string): number {
  const [hours, minutes] = pullTime.split(':').map(Number);
  return hours * 60 + minutes;
}

/**
 * True once both pull times are complete, well-formed HH:MM values and min doesn't precede max.
 * Gated on both sides already matching PULL_TIME_PATTERN so a mid-typing value (no colon yet,
 * one digit short) never reads as a range conflict -- only a genuinely finished pair can.
 */
export function isPullTimeRangeInvalid(minAcquisitionPullTime: string, maxAcquisitionPullTime: string): boolean {
  return Boolean(
    minAcquisitionPullTime &&
    maxAcquisitionPullTime &&
    PULL_TIME_PATTERN.test(minAcquisitionPullTime) &&
    PULL_TIME_PATTERN.test(maxAcquisitionPullTime) &&
    toMinutesSinceMidnight(minAcquisitionPullTime) >= toMinutesSinceMidnight(maxAcquisitionPullTime)
  );
}

export function validateFhir(
  values: FhirFieldValues,
  /** The pull-time field the user is actively editing, if any -- see the range check below. */
  editedPullTimeField?: 'minAcquisitionPullTime' | 'maxAcquisitionPullTime'
): FieldErrors {
  const errors: FieldErrors = {};

  const trimmedBaseUrl = values.fhirServerBaseUrl.trim();
  if (!trimmedBaseUrl) {
    errors.fhirServerBaseUrl = 'onboarding:fhirServerInfo.errors.baseUrlRequired';
  } else if (!isValidHttpUrl(trimmedBaseUrl)) {
    errors.fhirServerBaseUrl = 'onboarding:fhirServerInfo.messages.invalidBaseUrl';
  }

  if (values.maxConcurrentRequests == null) {
    errors.maxConcurrentRequests = 'onboarding:fhirServerInfo.messages.maxConcurrentRequestsRequired';
  } else if (
    !Number.isInteger(values.maxConcurrentRequests) ||
    values.maxConcurrentRequests < 1 ||
    values.maxConcurrentRequests > MAX_CONCURRENT_REQUESTS_CAP
  ) {
    errors.maxConcurrentRequests = 'onboarding:fhirServerInfo.messages.invalidMaxConcurrentRequests';
  }

  if (values.maxRetries != null &&
    (!Number.isInteger(values.maxRetries) || values.maxRetries < 0 || values.maxRetries > 10)) {
    errors.maxRetries = 'onboarding:fhirServerInfo.messages.invalidMaxRetries';
  }

  if (!values.minAcquisitionPullTime) {
    errors.minAcquisitionPullTime = 'onboarding:fhirServerInfo.errors.minPullTimeRequired';
  } else if (!PULL_TIME_PATTERN.test(values.minAcquisitionPullTime)) {
    errors.minAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.invalidPullTime';
  }

  if (!values.maxAcquisitionPullTime) {
    errors.maxAcquisitionPullTime = 'onboarding:fhirServerInfo.errors.maxPullTimeRequired';
  } else if (!PULL_TIME_PATTERN.test(values.maxAcquisitionPullTime)) {
    errors.maxAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.invalidPullTime';
  }

  // Only compare once both sides are individually well-formed - comparing against a pattern
  // failure would just bury the more useful "invalidPullTime" error under a confusing one.
  if (
    !errors.minAcquisitionPullTime &&
    !errors.maxAcquisitionPullTime &&
    isPullTimeRangeInvalid(values.minAcquisitionPullTime, values.maxAcquisitionPullTime)
  ) {
    // Wrong the moment the second time is entered, so - like a duplicate FHIR Patient ID or
    // census list id - only the field just edited carries the error, not both sides of the
    // pair. With no active edit (initial load, a blur on neither field), max defaults to the
    // flagged side.
    const flaggedField = editedPullTimeField ?? 'maxAcquisitionPullTime';
    errors[flaggedField] =
      flaggedField === 'maxAcquisitionPullTime'
        ? 'onboarding:fhirServerInfo.messages.pullTimeRangeInvalidMax'
        : 'onboarding:fhirServerInfo.messages.pullTimeRangeInvalid';
  }

  if (values.lagDays != null && (!Number.isInteger(values.lagDays) || values.lagDays < 0 || values.lagDays > LAG_DAYS_CAP)) {
    errors.lagDays = 'onboarding:fhirServerInfo.messages.invalidLagDays';
  }

  if (values.lagHours != null && (!Number.isInteger(values.lagHours) || values.lagHours < 0 || values.lagHours > 23)) {
    errors.lagHours = 'onboarding:fhirServerInfo.messages.invalidLagHours';
  }

  if (values.lagMinutes != null && (!Number.isInteger(values.lagMinutes) || values.lagMinutes < 0 || values.lagMinutes > 59)) {
    errors.lagMinutes = 'onboarding:fhirServerInfo.messages.invalidLagMinutes';
  }

  const lagFieldsValid = !errors.lagDays && !errors.lagHours && !errors.lagMinutes;
  if (lagFieldsValid && (values.lagDays ?? 0) + (values.lagHours ?? 0) + (values.lagMinutes ?? 0) === 0) {
    errors.lagDuration = 'onboarding:fhirServerInfo.messages.lagDurationRequired';
  }

  return errors;
}

// Inverse of FhirStep's buildIso8601Duration, but tolerant of any minimal ISO-8601 duration shape,
// not just the one that emits: a value round-tripped through Query Dispatch comes back as .NET's
// XmlConvert.ToString(TimeSpan), which drops any component that's zero entirely - 50 days with no
// hours/minutes serializes as "P50D", not "P50DT0H0M" - so every one of D/T/H/M/S here is optional,
// and a missing piece just means zero, not "unparseable".
export function parseIso8601Duration(duration?: string): [number | undefined, number | undefined, number | undefined] {
  if (!duration) {
    return [undefined, undefined, undefined];
  }

  const match = duration.match(/^P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:[\d.]+S)?)?$/);
  if (!match) {
    return [undefined, undefined, undefined];
  }

  return [Number(match[1] ?? 0), Number(match[2] ?? 0), Number(match[3] ?? 0)];
}

/** Mirrors FhirStep.validateStep: every field valid, and the current base URL actually tested. */
export function isFhirComplete(draft: FacilityDraft): boolean {
  const f = draft.fhir;
  if (!f.connectionTested) {
    return false;
  }
  const [lagDays, lagHours, lagMinutes] = parseIso8601Duration(f.lagDuration);
  const errors = validateFhir({
    fhirServerBaseUrl: f.fhirServerBaseUrl ?? '',
    maxConcurrentRequests: f.maxConcurrentRequests,
    maxRetries: f.maxRetries,
    minAcquisitionPullTime: f.minAcquisitionPullTime ?? '',
    maxAcquisitionPullTime: f.maxAcquisitionPullTime ?? '',
    lagDays,
    lagHours,
    lagMinutes
  });
  return Object.keys(errors).length === 0;
}
