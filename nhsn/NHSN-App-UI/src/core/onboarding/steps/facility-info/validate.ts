import type {FacilityDraft} from '../../types';

export interface FieldErrors {
  [field: string]: string;
}

export function validateFacilityInfo(draft: FacilityDraft): FieldErrors {
  const errors: FieldErrors = {};

  if (!draft.facilityInfo.timeZone) {
    errors.timeZone = 'onboarding:facilityInfo.messages.incomplete';
  }

  if (!draft.facilityInfo.vendor) {
    errors.vendor = 'onboarding:facilityInfo.messages.incomplete';
  }

  return errors;
}
