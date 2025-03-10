import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export function UniqueReportsValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const formGroup = control;
    if (!formGroup) return null; // If the form group is not yet available

    const monthlyReports = formGroup.get('monthlyReports')?.value || [];
    const dailyReports = formGroup.get('dailyReports')?.value || [];
    const weeklyReports = formGroup.get('weeklyReports')?.value || [];

    const allReports = [...monthlyReports, ...dailyReports, ...weeklyReports];

    const uniqueReports = new Set(allReports);

    if (allReports.length !== uniqueReports.size) {
      // Return error if there are duplicates
      return { reportsNotUnique: 'The scheduled reports must be unique across all periods (monthly, daily, weekly)' };
    }

    // Return null if valid (no duplicates)
    return null;
  };
}
