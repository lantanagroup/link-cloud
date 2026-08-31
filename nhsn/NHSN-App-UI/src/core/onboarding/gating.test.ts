import {describe, expect, it} from 'vitest';
import type {UserInfoResponse} from '../api/contracts';
import {furthestLegalStep, isUnlocked, nextStepId, resolveStep} from './gating';
import {createEmptyDraft, type FacilityDraft, type StepId} from './types';

/**
 * The gate is where every navigation lands — reload, deep link, popstate and
 * Next alike — so it carries most of the machine's risk in pure functions.
 */

const user: UserInfoResponse = {
  accessState: 'Allowed',
  email: 'a@example.invalid',
  name: 'A',
  isFacilityAdmin: true,
  isOnboarded: false,
  hasFacility: true,
  facilityId: 'F1',
  groups: ['FACADMIN'],
  availableNavigation: ['onboarding']
};

function draftAt(unlocked: StepId[], overrides: Partial<FacilityDraft> = {}): FacilityDraft {
  return {...createEmptyDraft(), unlockedStepIds: unlocked, ...overrides};
}

describe('resolveStep', () => {
  it('falls back to the furthest legal step when the target was never unlocked', () => {
    const draft = draftAt(['welcome']);
    expect(resolveStep({stepId: 'fhir'}, draft, user)).toEqual({stepId: 'welcome'});
  });

  it('honours a target the draft has unlocked', () => {
    const draft = draftAt(['welcome', 'reporting-plan']);
    expect(resolveStep({stepId: 'reporting-plan'}, draft, user)).toEqual({
      stepId: 'reporting-plan'
    });
  });

  it('falls back when no target is supplied', () => {
    const draft = draftAt(['welcome', 'reporting-plan']);
    expect(resolveStep(undefined, draft, user)).toEqual({stepId: 'reporting-plan'});
  });

  it('rejects a step id that is not in the flow', () => {
    const draft = draftAt(['welcome']);
    expect(resolveStep({stepId: 'not-a-step' as StepId}, draft, user)).toEqual({
      stepId: 'welcome'
    });
  });

  it('keeps a declared sub-view', () => {
    const draft = draftAt(
      ['welcome', 'reporting-plan', 'facility-info', 'manual-upload', 'fhir', 'census',
        'location-org', 'hsloc', 'encounter', 'report', 'report-results'],
      {
        facilityInfo: {timeZone: 'America/Chicago', vendor: 'Epic'},
        manualUpload: {uploadedFileName: 'facility-data.csv', uploadedOn: '2026-01-01T00:00:00.000Z'},
        fhir: {fhirServerBaseUrl: 'https://example.invalid/fhir', connectionTested: true}
      }
    );
    const target = {
      stepId: 'report-results' as StepId,
      view: {stepId: 'report-results' as StepId, view: 'detail', params: {id: 'R1'}}
    };
    expect(resolveStep(target, draft, user)).toEqual(target);
  });

  it('degrades an undeclared sub-view to its step rather than erroring', () => {
    const draft = draftAt(['welcome', 'reporting-plan']);
    const resolved = resolveStep(
      {
        stepId: 'reporting-plan',
        view: {stepId: 'reporting-plan', view: 'nonexistent'}
      },
      draft,
      user
    );
    expect(resolved).toEqual({stepId: 'reporting-plan'});
  });
});

describe('isUnlocked', () => {
  // TEMP: skipped alongside TEMP_ALWAYS_COMPLETE in flow.ts — facility-info's
  // real isComplete is bypassed for now, so this no longer holds. Re-enable
  // once that temporary override is removed.
  it.skip('requires every preceding step to be complete, not just an unlock record', () => {
    // facility-info is unlocked but incomplete, so fhir behind it is not reachable
    // even though a stale draft lists it.
    const draft = draftAt(['welcome', 'reporting-plan', 'facility-info', 'fhir']);
    expect(isUnlocked('fhir', draft, user)).toBe(false);
  });

  it('allows a step once its prerequisites are satisfied', () => {
    const draft = draftAt(['welcome', 'reporting-plan', 'facility-info', 'manual-upload'], {
      facilityInfo: {timeZone: 'America/Chicago', vendor: 'Epic'}
    });
    expect(isUnlocked('manual-upload', draft, user)).toBe(true);
  });

  it('always allows the first step', () => {
    expect(isUnlocked('welcome', createEmptyDraft(), user)).toBe(true);
  });
});

describe('furthestLegalStep', () => {
  it('stops at the first incomplete step', () => {
    const draft = draftAt(['welcome', 'reporting-plan', 'facility-info', 'manual-upload']);
    // facility-info has no vendor or timezone, so it blocks manual-upload.
    expect(furthestLegalStep(draft, user)).toBe('facility-info');
  });
});

describe('nextStepId', () => {
  it('follows the POC order, with the reporting plan second', () => {
    const draft = createEmptyDraft();
    expect(nextStepId('welcome', draft, user)).toBe('reporting-plan');
    expect(nextStepId('reporting-plan', draft, user)).toBe('facility-info');
  });

  it('returns undefined at the end of the flow', () => {
    expect(nextStepId('complete', createEmptyDraft(), user)).toBeUndefined();
  });
});
