import {AbstractControl, ValidationErrors, ValidatorFn} from "@angular/forms";

export function resourceTypeMatchValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const selectedType = control.get('selectedResourceType')?.value;
    const jsonText = control.get('resourceJson')?.value;

    if (!selectedType || !jsonText) {
      return null; // No validation if either is missing
    }

    try {
      const jsonObj = JSON.parse(jsonText);
      const typeInJson = jsonObj.resourceType;

      if (typeInJson && typeInJson !== selectedType) {
        return { resourceTypeMismatch: true };
      }
    } catch (e) {
      // Invalid JSON? Ignore here — let JSON validation handle that.
      return null;
    }

    return null;
  };
}
