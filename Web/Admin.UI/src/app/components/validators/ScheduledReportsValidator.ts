import {AbstractControl, ValidationErrors, ValidatorFn} from '@angular/forms';

export function ScheduledReportsValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const formGroup = control;
    if (!formGroup) return null; // If the form group is not yet available

    const monthlyReports = formGroup.get('monthlyReports')?.value || [];
    const dailyReports = formGroup.get('dailyReports')?.value || [];
    const weeklyReports = formGroup.get('weeklyReports')?.value || [];

    const allReports = [...monthlyReports, ...dailyReports, ...weeklyReports];

    const uniqueReports = new Set(allReports);

    const errors: ValidationErrors = {};

    // Check if at least one report is entered
    if (allReports.length === 0) {
      errors["noReportsEntered"] = 'At least one report must be entered.';
    }

    // Check for duplicate reports
    if (allReports.length !== uniqueReports.size) {
      errors["reportsNotUnique"] = 'The scheduled reports must be unique across all periods (monthly, daily, weekly).';
    }

    // Return errors if any, otherwise null
    return Object.keys(errors).length > 0 ? errors : null;
  };
}
