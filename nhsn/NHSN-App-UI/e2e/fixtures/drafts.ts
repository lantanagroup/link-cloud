import {createEmptyDraft, STEP_IDS, type FacilityDraft, type StepId} from '../../src/core/onboarding/types';

/**
 * Typed draft builders, against `src/core/onboarding/types` rather than loose literals, so a
 * change to `FacilityDraft` breaks `npm run typecheck:e2e` instead of a spec at runtime.
 *
 * Seeding a draft is how a spec reaches a late step. Clicking through twelve steps per spec
 * would be both slow and a worse test — it couples every spec to every earlier step's form.
 */

/**
 * Where `MockApiClient` keeps the draft: `DRAFT_KEY_PREFIX` + its facility id. The harness
 * builds `new MockApiClient(activeProfile?.facilityId, …)`, so with no saved test profile
 * the constructor default applies and this is the key. Seed a profile and the key changes.
 */
export const MOCK_DRAFT_KEY = 'nhsn-app-ui.mockDraft.MOCK-FACILITY-001';

export const MOCK_FACILITY_ID = 'MOCK-FACILITY-001';
export const MOCK_FACILITY_NAME = 'Mock Facility';

/**
 * A draft sitting on `stepId` with every step up to it unlocked.
 *
 * Unlocking alone is not enough to land there: `isUnlocked` also requires every earlier
 * visible step to be *complete* (gating.ts), so a caller must supply the slice data those
 * steps validate — `facilityInfoComplete()` and friends below. Under-fill it and
 * `resolveStep` silently drops you back to `furthestLegalStep`, which reads as "the deep
 * link is broken" when it is the fixture that is.
 */
export function draftAt(stepId: StepId, overrides: Partial<FacilityDraft> = {}): FacilityDraft {
  return {
    ...createEmptyDraft(),
    ...overrides,
    currentStepId: stepId,
    // Declaration order is flow order, and it is the app's own list — not a copy to drift.
    unlockedStepIds: STEP_IDS.slice(0, STEP_IDS.indexOf(stepId) + 1) as StepId[]
  };
}

/** The two fields `isFacilityInfoComplete` requires. Epic, so vendor-branching copy is Epic's. */
export function facilityInfoComplete(): FacilityDraft['facilityInfo'] {
  return {vendor: 'Epic', timeZone: 'America/New_York'};
}

/** What `isFhirComplete` requires: every field valid **and** the base URL actually tested. */
export function fhirComplete(): FacilityDraft['fhir'] {
  return {
    fhirServerBaseUrl: 'https://ehr.example.org/fhir/r4',
    maxConcurrentRequests: 4,
    lagDuration: 'PT30M',
    connectionTested: true
  };
}
