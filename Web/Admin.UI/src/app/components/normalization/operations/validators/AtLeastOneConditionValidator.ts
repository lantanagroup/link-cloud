import {AbstractControl, FormArray, ValidationErrors} from '@angular/forms';

export function AtLeastOneConditionValidator(control: AbstractControl): ValidationErrors | null {
  const formArray = control as FormArray; // or as FormArray
  return formArray && formArray.length > 0 ? null : { atLeastOneRequired: true };
}


