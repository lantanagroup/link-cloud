import {AbstractControl, ValidationErrors, ValidatorFn} from '@angular/forms';

/**
 * Validates the scheduled reports on the facility form.
 *
 * @param dmrpEnabled DMRP feature flag. When DMRP is enabled the facility's schedule is derived from
 * its DMRP reporting plans rather than chosen here, and the Tenant API refuses a facility that
 * supplies one, so the form must be submittable with no reports selected. Duplicates are rejected
 * either way. Defaults to the state this flag is expected to settle on, so that retiring it means
 * deleting the parameter and the one check below that reads it — see AppConfig.dmrpEnabled.
 */
export function ScheduledReportsValidator(dmrpEnabled: boolean = true): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const formGroup = control;
    if (!formGroup) return null; // If the form group is not yet available

    const monthlyReports = formGroup.get('monthlyReports')?.value || [];
    const dailyReports = formGroup.get('dailyReports')?.value || [];
    const weeklyReports = formGroup.get('weeklyReports')?.value || [];

    const allReports = [...monthlyReports, ...dailyReports, ...weeklyReports];

    const uniqueReports = new Set(allReports);

    const errors: ValidationErrors = {};

    // Check if at least one report is entered. DMRP feature flag: this whole check goes when the
    // flag is retired, because a derived schedule is never chosen on this form.
    if (!dmrpEnabled && allReports.length === 0) {
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
