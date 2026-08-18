import {FormControl, FormGroup} from '@angular/forms';
import {ScheduledReportsValidator} from './ScheduledReportsValidator';

/**
 * The "at least one report" rule has to yield when DMRP is enabled: the schedule is derived from the
 * facility's DMRP reporting plans, and the Tenant API refuses a facility that supplies its own. With
 * the rule always on there is no input the form can produce that the API accepts.
 */
describe('ScheduledReportsValidator', () => {
  function formWith(monthly: string[] = [], daily: string[] = [], weekly: string[] = []): FormGroup {
    return new FormGroup({
      monthlyReports: new FormControl(monthly),
      dailyReports: new FormControl(daily),
      weeklyReports: new FormControl(weekly)
    });
  }

  describe('with DMRP disabled', () => {
    const validate = ScheduledReportsValidator(false);

    it('requires at least one report', () => {
      const errors = validate(formWith());

      expect(errors?.['noReportsEntered']).toBeTruthy();
    });

    it('accepts a single report', () => {
      expect(validate(formWith(['measure-a']))).toBeNull();
    });
  });

  describe('with DMRP enabled', () => {
    const validate = ScheduledReportsValidator(true);

    it('accepts a facility with no reports', () => {
      expect(validate(formWith())).toBeNull();
    });
  });

  it('rejects a report repeated across periods whether or not DMRP is enabled', () => {
    for (const dmrpEnabled of [false, true]) {
      const errors = ScheduledReportsValidator(dmrpEnabled)(formWith(['measure-a'], ['measure-a']));

      expect(errors?.['reportsNotUnique']).toBeTruthy();
    }
  });

  /**
   * The parameter defaults to the state the DMRP flag is expected to settle on, so that retiring it
   * is a deletion rather than a behavior change.
   */
  it('behaves as though DMRP is enabled when called with no argument', () => {
    expect(ScheduledReportsValidator()(formWith())).toBeNull();
  });
});
