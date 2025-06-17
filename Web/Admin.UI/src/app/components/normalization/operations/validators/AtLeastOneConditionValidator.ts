import { AbstractControl, ValidationErrors } from '@angular/forms';

export function AtLeastOneConditionValidator(control: AbstractControl): ValidationErrors | null {
  const formArray = control as any; // or as FormArray
  return formArray && formArray.length > 0 ? null : { atLeastOneRequired: true };
}


