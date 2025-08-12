import {AbstractControl, ValidationErrors} from "@angular/forms";

export  function validJsonValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) return null; // empty input is valid if optional

  try {
    JSON.parse(value);
    return null; // valid JSON
  } catch {
    return { invalidJson: true };
  }
}
