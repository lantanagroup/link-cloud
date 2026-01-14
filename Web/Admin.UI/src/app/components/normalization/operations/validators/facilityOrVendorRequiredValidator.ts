import {AbstractControl, FormArray, ValidationErrors, ValidatorFn} from '@angular/forms';

export const facilityOrVendorRequiredValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const vendor = control.get('selectedVendor')?.value;
  const facility = control.get('facilityId')?.value;

  const hasVendor = Array.isArray(vendor) ? vendor.length > 0 : !!vendor;
  const hasFacility = !!facility;

  return hasVendor || hasFacility ? null : { facilityOrVendorRequired: true };
};
