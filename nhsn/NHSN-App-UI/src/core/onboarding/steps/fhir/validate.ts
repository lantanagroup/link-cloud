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

export function isValidHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

/** 24-hour HH:MM, hours 00-23, minutes 00-59 — what `normalizePullTime` in FhirStep produces on blur. */
const PULL_TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export function validateFhir(values: FhirFieldValues): FieldErrors {
  const errors: FieldErrors = {};

  const trimmedBaseUrl = values.fhirServerBaseUrl.trim();
  if (!trimmedBaseUrl) {
    errors.fhirServerBaseUrl = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!isValidHttpUrl(trimmedBaseUrl)) {
    errors.fhirServerBaseUrl = 'onboarding:fhirServerInfo.messages.invalidBaseUrl';
  }

  if (values.maxConcurrentRequests === undefined) {
    errors.maxConcurrentRequests = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!Number.isInteger(values.maxConcurrentRequests) || values.maxConcurrentRequests < 1) {
    errors.maxConcurrentRequests = 'onboarding:fhirServerInfo.messages.invalidMaxConcurrentRequests';
  }

  if (values.maxRetries === undefined) {
    errors.maxRetries = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!Number.isInteger(values.maxRetries) || values.maxRetries < 0 || values.maxRetries > 10) {
    errors.maxRetries = 'onboarding:fhirServerInfo.messages.invalidMaxRetries';
  }

  if (!values.minAcquisitionPullTime) {
    errors.minAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!PULL_TIME_PATTERN.test(values.minAcquisitionPullTime)) {
    errors.minAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.invalidPullTime';
  }

  if (!values.maxAcquisitionPullTime) {
    errors.maxAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!PULL_TIME_PATTERN.test(values.maxAcquisitionPullTime)) {
    errors.maxAcquisitionPullTime = 'onboarding:fhirServerInfo.messages.invalidPullTime';
  }

  if (values.lagDays === undefined) {
    errors.lagDays = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!Number.isInteger(values.lagDays) || values.lagDays < 0) {
    errors.lagDays = 'onboarding:fhirServerInfo.messages.invalidLagDays';
  }

  if (values.lagHours === undefined) {
    errors.lagHours = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!Number.isInteger(values.lagHours) || values.lagHours < 0 || values.lagHours > 23) {
    errors.lagHours = 'onboarding:fhirServerInfo.messages.invalidLagHours';
  }

  if (values.lagMinutes === undefined) {
    errors.lagMinutes = 'onboarding:fhirServerInfo.messages.incomplete';
  } else if (!Number.isInteger(values.lagMinutes) || values.lagMinutes < 0 || values.lagMinutes > 59) {
    errors.lagMinutes = 'onboarding:fhirServerInfo.messages.invalidLagMinutes';
  }

  return errors;
}
