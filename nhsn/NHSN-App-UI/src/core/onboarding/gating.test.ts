import {describe, expect, it} from 'vitest';
import type {UserInfoResponse, VendorProfile} from '../api/contracts';
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
  return {
    ...createEmptyDraft(),
    unlockedStepIds: unlocked,
    ...overrides
  };
}

const epicProfile: VendorProfile = {
  vendor: 'Epic',
  displayName: 'Epic',
  censusAcquisition: 'PatientList',
  patientListKeys: [
    'admit-lt-24',
    'admit-24-to-48',
    'admit-gt-48',
    'discharge-lt-24',
    'discharge-24-to-48',
    'discharge-gt-48'
  ],
  locationMethods: [],
  documentKeys: {},
  hslocSourceLabel: 'Epic Location'
};

const cernerProfile: VendorProfile = {
  vendor: 'Cerner',
  displayName: 'Cerner',
  censusAcquisition: 'Sftp',
  patientListKeys: [],
  locationMethods: [],
  documentKeys: {},
  hslocSourceLabel: 'Cerner Location'
};

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
        fhir: {
          fhirServerBaseUrl: 'https://example.invalid/fhir',
          maxConcurrentRequests: 4,
          lagDuration: 'P0DT1H0M',
          connectionTested: true
        },
        census: {
          patientListIds: {
            'admit-lt-24': 'list-1',
            'admit-24-to-48': 'list-2',
            'admit-gt-48': 'list-3',
            'discharge-lt-24': 'list-4',
            'discharge-24-to-48': 'list-5',
            'discharge-gt-48': 'list-6'
          },
          acquisitionFrequency: 'PT0H15M',
          accuracyAcknowledged: true
        },
        hsloc: {mappings: [{sourceCode: 'ICU', sourceDisplay: 'ICU', hslocCode: '1024-9'}]},
        report: {lastRequestedReportId: 'R1'}
      }
    );
    const target = {
      stepId: 'report-results' as StepId,
      view: {stepId: 'report-results' as StepId, view: 'detail', params: {id: 'R1'}}
    };
    expect(resolveStep(target, draft, user, epicProfile)).toEqual(target);
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
  it('requires every preceding step to be complete, not just an unlock record', () => {
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

  it('unlocks every step once onboarded, when the OnboardingRevisit capability is on', () => {
    const onboardedUser: UserInfoResponse = {
      ...user,
      isOnboarded: true,
      capabilities: {
        patientListWithNames: false,
        fhirConnectionProbe: false,
        sftpFileListing: false,
        onboardingRevisit: true
      }
    };
    // Never unlocked, never observed as complete -- still reachable in revisit mode.
    expect(isUnlocked('report-results', createEmptyDraft(), onboardedUser)).toBe(true);
  });

  it('does not unlock every step just from being onboarded, without the capability', () => {
    const onboardedUser: UserInfoResponse = {...user, isOnboarded: true};
    expect(isUnlocked('report-results', createEmptyDraft(), onboardedUser)).toBe(false);
  });

  it('does not unlock every step from the capability alone, before onboarding completes', () => {
    const flaggedUser: UserInfoResponse = {
      ...user,
      isOnboarded: false,
      capabilities: {
        patientListWithNames: false,
        fhirConnectionProbe: false,
        sftpFileListing: false,
        onboardingRevisit: true
      }
    };
    expect(isUnlocked('report-results', createEmptyDraft(), flaggedUser)).toBe(false);
  });

  it('does not let a previous vendor\'s leftover census fields block the current vendor\'s step', () => {
    const draft = draftAt(
      ['welcome', 'reporting-plan', 'facility-info', 'manual-upload', 'fhir', 'census', 'location-org'],
      {
        facilityInfo: {timeZone: 'America/Chicago', vendor: 'Epic'},
        manualUpload: {uploadedFileName: 'facility-data.csv', uploadedOn: '2026-01-01T00:00:00.000Z'},
        fhir: {fhirServerBaseUrl: 'https://example.invalid/fhir', connectionTested: true},
        census: {
          patientListIds: {
            'admit-lt-24': 'list-1',
            'admit-24-to-48': 'list-2',
            'admit-gt-48': 'list-3',
            'discharge-lt-24': 'list-4',
            'discharge-24-to-48': 'list-5',
            'discharge-gt-48': 'list-6'
          },
          acquisitionFrequency: 'PT0H15M',
          accuracyAcknowledged: true,
          // Leftover from the earlier Cerner configuration - never cleared client-side.
          sftpHost: 'old-cerner-host.example.invalid',
          sftpPort: 22,
          sftpConnectionTested: false
        }
      }
    );
    expect(isUnlocked('location-org', draft, user, epicProfile)).toBe(true);
  });

  it('still requires a tested connection for a facility actually on Cerner', () => {
    const draft = draftAt(
      ['welcome', 'reporting-plan', 'facility-info', 'manual-upload', 'fhir', 'census', 'location-org'],
      {
        facilityInfo: {timeZone: 'America/Chicago', vendor: 'Cerner'},
        manualUpload: {uploadedFileName: 'facility-data.csv', uploadedOn: '2026-01-01T00:00:00.000Z'},
        fhir: {fhirServerBaseUrl: 'https://example.invalid/fhir', connectionTested: true},
        census: {
          sftpHost: 'sftp.example.invalid',
          sftpPort: 22,
          sftpConnectionTested: false,
          acquisitionFrequency: 'PT0H15M',
          accuracyAcknowledged: true
        }
      }
    );
    expect(isUnlocked('location-org', draft, user, cernerProfile)).toBe(false);
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
