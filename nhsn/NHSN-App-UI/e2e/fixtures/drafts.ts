import {CENSUS_LIST_KEYS} from '../../src/core/onboarding/steps/census/validate';
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

/**
 * What `census.isComplete` requires of an Epic (PatientList) facility: all six list ids and a
 * frequency inside the 5-minute/24-hour window. No `accuracyAcknowledged` — that is only
 * demanded when `capabilities.patientListWithNames` is on, and the mock leaves it off.
 *
 * Keys come from the app's own `CENSUS_LIST_KEYS` so a change to the list breaks the
 * typecheck here rather than leaving a fixture that under-fills the gate.
 */
export function censusComplete(): FacilityDraft['census'] {
  return {
    patientListIds: Object.fromEntries(
      CENSUS_LIST_KEYS.map((key, index) => [key, `epic-list-${index + 1}`])
    ),
    acquisitionFrequency: 'PT1H0M'
  };
}

/**
 * One complete HSLOC mapping, which is all `isHslocComplete` asks for while
 * `capabilities.hslocLocationDisplayUpdate` is off (no `sourceDisplay` required).
 *
 * Note this unlocks the steps after HSLOC but does **not** show up on the HSLOC panel itself:
 * that panel hydrates from `api.getHslocMappings()`, and the mock persists nothing.
 */
export function hslocComplete(): FacilityDraft['hsloc'] {
  return {mappings: [{sourceCode: 'ICU-1', hslocCode: '1027-2'}]};
}

/**
 * The encounter step's gate is `() => true`, so this exists for the panel rather than the gate:
 * unlike HSLOC, the encounter step builds its groups from the draft, so a seeded code system is
 * what a later step's fixture needs to look like a facility that has been through it.
 */
export function encounterComplete(): FacilityDraft['encounter'] {
  return {codeSystems: ['http://mysite.org/fhir/encounter-type'], mappings: []};
}

/** The id the mock's `requestReport` always assigns. */
export const MOCK_REPORT_ID = 'SIMULATED-REPORT-0001';

/**
 * What `isReportComplete` requires: a report that has actually been requested. The parameters
 * alone are not enough — nothing has reached Link until the request is made.
 */
export function reportComplete(): FacilityDraft['report'] {
  return {
    measures: ['Glycemic Control (simulated)'],
    startDate: '2026-10-01',
    endDate: '2026-10-31',
    patientIds: ['patient-001'],
    lastRequestedReportId: MOCK_REPORT_ID
  };
}

/**
 * The acknowledgement that unlocks MRN Intake. Seeding it is the **only** way past Report Results
 * in mock mode: the checkbox is disabled until a report appears in `listReports`, and the mock's
 * `listReports` always returns an empty page.
 */
export function reportResultsAcknowledged(): FacilityDraft['reportResults'] {
  return {
    accuracyAcknowledged: true,
    requestedMeasuresByReportId: {[MOCK_REPORT_ID]: ['Glycemic Control (simulated)']}
  };
}

/**
 * A completed MRN intake, as the step saves it for a facility that answered No to all three
 * questions. `isMrnIntakeComplete` reads this mirror, so without it the final step is not legal
 * and a deep link to it falls back to MRN Intake.
 *
 * One rule, because the step requires one whatever the answers are — see
 * `mrn-intake-step.spec.ts`.
 */
export function mrnIntakeComplete(): FacilityDraft['mrnIntake'] {
  return {
    hasMultipleMrn: false,
    multipleMrnTypes: [],
    userFacingIdentifierNames: [],
    isCalledMrn: true,
    canSearchByIdentifier: true,
    variesByFacility: false,
    varianceTypes: [],
    changesOverTime: false,
    changeTypes: [],
    rules: [{ordinal: 0, element: 'system', rule: 'equals', value: 'http://hospital.org/fhir/identifier/mrn'}],
    observations: []
  };
}
