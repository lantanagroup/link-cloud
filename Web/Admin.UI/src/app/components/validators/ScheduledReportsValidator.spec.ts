import {FormControl, FormGroup} from '@angular/forms';
import {ScheduledReportsValidator} from './ScheduledReportsValidator';

describe('ScheduledReportsValidator', () => {
  function formWith(monthly: string[] = [], daily: string[] = [], weekly: string[] = []): FormGroup {
    return new FormGroup({
      monthlyReports: new FormControl(monthly),
      dailyReports: new FormControl(daily),
      weeklyReports: new FormControl(weekly)
    });
  }

  const validate = ScheduledReportsValidator();

  /**
   * The form used to require one. A facility scheduled for nothing is valid whether its schedule is
   * derived from DMRP reporting plans or simply left empty.
   */
  it('accepts a facility with no reports', () => {
    expect(validate(formWith())).toBeNull();
  });

  it('accepts a single report', () => {
    expect(validate(formWith(['measure-a']))).toBeNull();
  });

  it('rejects a report repeated across periods', () => {
    const errors = validate(formWith(['measure-a'], ['measure-a']));

    expect(errors?.['reportsNotUnique']).toBeTruthy();
  });

  it('rejects a report repeated within one period', () => {
    const errors = validate(formWith(['measure-a', 'measure-a']));

    expect(errors?.['reportsNotUnique']).toBeTruthy();
  });
});
