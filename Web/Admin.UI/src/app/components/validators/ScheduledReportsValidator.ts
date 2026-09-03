import {AbstractControl, ValidationErrors, ValidatorFn} from '@angular/forms';

/**
 * Validates the scheduled reports on the facility form.
 *
 * A facility is not required to name any report. That rule predated DMRP and has been removed
 * outright: with DMRP enabled the schedule is derived from the facility's reporting plans rather
 * than chosen here, and with it disabled a facility scheduled for nothing is still a legitimate
 * thing to save. Duplicates remain rejected — the Tenant API refuses a schedule naming one twice.
 */
export function ScheduledReportsValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const formGroup = control;
    if (!formGroup) return null; // If the form group is not yet available

    const monthlyReports = formGroup.get('monthlyReports')?.value || [];
    const dailyReports = formGroup.get('dailyReports')?.value || [];
    const weeklyReports = formGroup.get('weeklyReports')?.value || [];

    const allReports = [...monthlyReports, ...dailyReports, ...weeklyReports];
    const uniqueReports = new Set(allReports);

    if (allReports.length !== uniqueReports.size) {
      return {
        reportsNotUnique: 'The scheduled reports must be unique across all periods (monthly, daily, weekly).'
      };
    }

    return null;
  };
}
