import {describe, expect, it} from 'vitest';
import {createEmptyDraft} from '../../types';
import {isReportingPlanComplete} from './validate';

describe('isReportingPlanComplete', () => {
  it('is incomplete before the step has fetched anything', () => {
    expect(isReportingPlanComplete(createEmptyDraft())).toBe(false);
  });

  it('is incomplete when the fetch found no available measures', () => {
    const draft = createEmptyDraft();
    draft.reportingPlan.hasAvailableMeasure = false;
    expect(isReportingPlanComplete(draft)).toBe(false);
  });

  it('is complete once at least one available measure was found', () => {
    const draft = createEmptyDraft();
    draft.reportingPlan.hasAvailableMeasure = true;
    expect(isReportingPlanComplete(draft)).toBe(true);
  });
});
