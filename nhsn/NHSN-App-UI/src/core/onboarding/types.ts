import type {
  CensusListKey,
  EhrVendor,
  EncounterMapping,
  HslocMapping,
  LocationMethod,
  MrnIntake,
  ReportStatus
} from '../api/contracts';

export const STEP_IDS = [
  'welcome',
  'reporting-plan',
  'facility-info',
  'manual-upload',
  'fhir',
  'census',
  'location-org',
  'hsloc',
  'encounter',
  'report',
  'report-results',
  'mrn-intake',
  'complete'
] as const;

export type StepId = (typeof STEP_IDS)[number];

export function isStepId(value: string): value is StepId {
  return (STEP_IDS as readonly string[]).includes(value);
}

/**
 * A drill-down inside a step. Today only Report Details uses one — it is
 * reached from the Report Results table and keeps that step's id, so a reload
 * inside the drill-down must return there rather than to the top of the step.
 */
export interface StepView {
  stepId: StepId;
  view: string;
  params?: Record<string, string>;
}

export interface StepTarget {
  stepId: StepId;
  view?: StepView;
}

/**
 * Bump when a change to FacilityDraft is not readable by the previous shape,
 * and add a migration in migrateDraft(). Browsers cache nhsn-link.js
 * independently of BFF deploys, so an older bundle may read a newer draft.
 */
export const DRAFT_SCHEMA_VERSION = 2;

// ---------------------------------------------------------------- step slices

export interface FacilityInfoDraft {
  timeZone?: string;
  vendor?: EhrVendor;
}

export interface FhirDraft {
  fhirServerBaseUrl?: string;
  maxConcurrentRequests?: number;
  maxRetries?: number;
  minAcquisitionPullTime?: string;
  maxAcquisitionPullTime?: string;
  lagDuration?: string;
  connectionTested?: boolean;
}

export interface CensusDraft {
  /** Epic: FHIR List id per key. Absent for Cerner. */
  patientListIds?: Partial<Record<CensusListKey, string>>;
  /** Cerner sFTP connection. Credentials are NEVER stored here — see below. */
  sftpHost?: string;
  sftpPort?: number;
  sftpRemoteDirectory?: string;
  sftpRemoveAfterProcessing?: boolean;
  /**
   * Whether credentials have been posted to the BFF. The credentials
   * themselves go to saveSftpCredentials() and are forwarded to Data
   * Acquisition's credential store — the draft is persisted, versioned,
   * returned on every read and exported to a spreadsheet, so nothing secret
   * may enter it.
   */
  hasCredentials?: boolean;
  acquisitionFrequency?: string;
  accuracyAcknowledged?: boolean;
}

/** One row of the Location Type / Location Alias list. */
export interface LocationTypeEntry {
  code: string;
  alias: string;
}

/** One row of the Location Identifier System / Code list. */
export interface LocationIdentifierEntry {
  system: string;
  code: string;
}

export interface LocationOrgDraft {
  method?: LocationMethod;
  managingOrganizationIds?: string[];
  /** Schema version 2 — see migrateDraft for the version 1 shape. */
  locationTypes?: LocationTypeEntry[];
  locationIdentifiers?: LocationIdentifierEntry[];
  customFhirPath?: string;
}

export interface HslocDraft {
  mappings?: HslocMapping[];
}

export interface EncounterDraft {
  codeSystems?: string[];
  mappings?: EncounterMapping[];
}

export interface ReportDraft {
  measures?: string[];
  startDate?: string;
  endDate?: string;
  patientIds?: string[];
  lastRequestedReportId?: string;
}

export interface ReportResultsDraft {
  viewingReportId?: string;
  accuracyAcknowledged?: boolean;
  latestStatus?: ReportStatus;
}

export interface ManualUploadDraft {
  uploadedFileName?: string;
  uploadedOn?: string;
}

export interface ReportingPlanDraft {
  reviewed?: boolean;
}

// ---------------------------------------------------------------- the draft

/**
 * All onboarding state, in one shape.
 *
 * Defined in one file rather than assembled from per-step slices: the reducer
 * operates on the whole shape, it is persisted and versioned as a unit, and
 * splitting it across step folders invites import cycles.
 *
 * Three things deliberately live outside it — secrets, MRN intake (normalized
 * server-side and mirrored here for rendering only) and reference data.
 */
export interface FacilityDraft {
  schemaVersion: number;
  currentStepId: StepId;
  currentView?: StepView;
  /** Steps the user has reached. Gating also requires prerequisites (gating.ts). */
  unlockedStepIds: StepId[];

  facilityInfo: FacilityInfoDraft;
  manualUpload: ManualUploadDraft;
  fhir: FhirDraft;
  census: CensusDraft;
  locationOrg: LocationOrgDraft;
  hsloc: HslocDraft;
  encounter: EncounterDraft;
  report: ReportDraft;
  reportResults: ReportResultsDraft;
  reportingPlan: ReportingPlanDraft;
  /** Read-only mirror. The MRN step writes through the API, not the reducer. */
  mrnIntake?: MrnIntake;
}

export function createEmptyDraft(): FacilityDraft {
  return {
    schemaVersion: DRAFT_SCHEMA_VERSION,
    currentStepId: 'welcome',
    unlockedStepIds: ['welcome'],
    facilityInfo: {},
    manualUpload: {},
    fhir: {},
    census: {},
    locationOrg: {},
    hsloc: {},
    encounter: {},
    report: {},
    reportResults: {},
    reportingPlan: {}
  };
}

/**
 * Applied on every read. An older bundle must survive a newer draft and vice
 * versa, so this fills gaps rather than rejecting what it does not recognize.
 */
export function migrateDraft(raw: unknown): FacilityDraft {
  const empty = createEmptyDraft();
  if (!raw || typeof raw !== 'object') {
    return empty;
  }

  const incoming = raw as Partial<FacilityDraft>;
  const merged: FacilityDraft = {
    ...empty,
    ...incoming,
    schemaVersion: DRAFT_SCHEMA_VERSION,
    facilityInfo: {...empty.facilityInfo, ...incoming.facilityInfo},
    manualUpload: {...empty.manualUpload, ...incoming.manualUpload},
    fhir: {...empty.fhir, ...incoming.fhir},
    census: {...empty.census, ...incoming.census},
    locationOrg: migrateLocationOrg(empty.locationOrg, incoming.locationOrg),
    hsloc: {...empty.hsloc, ...incoming.hsloc},
    encounter: {...empty.encounter, ...incoming.encounter},
    report: {...empty.report, ...incoming.report},
    reportResults: {...empty.reportResults, ...incoming.reportResults},
    reportingPlan: {...empty.reportingPlan, ...incoming.reportingPlan}
  };

  // A step id retired by a later release must not strand the user.
  merged.unlockedStepIds = (incoming.unlockedStepIds ?? empty.unlockedStepIds).filter(isStepId);
  if (!merged.unlockedStepIds.includes('welcome')) {
    merged.unlockedStepIds.unshift('welcome');
  }
  if (!isStepId(merged.currentStepId)) {
    merged.currentStepId = 'welcome';
  }
  if (merged.currentView && !isStepId(merged.currentView.stepId)) {
    delete merged.currentView;
  }

  return merged;
}

/** Schema version 1 -> 2: `locationTypeCodes`/`locationIdentifiers` (string[]) become pairs. */
function migrateLocationOrg(
  empty: LocationOrgDraft,
  incoming: LocationOrgDraft | undefined
): LocationOrgDraft {
  const legacy = incoming as (LocationOrgDraft & {locationTypeCodes?: unknown}) | undefined;
  const merged: LocationOrgDraft = {...empty, ...incoming};

  const legacyTypeCodes = asStringList(legacy?.locationTypeCodes);
  if (!merged.locationTypes && legacyTypeCodes) {
    merged.locationTypes = legacyTypeCodes.map(code => ({code, alias: ''}));
  }

  const legacyIdentifiers = asStringList(legacy?.locationIdentifiers);
  if (legacyIdentifiers) {
    merged.locationIdentifiers = legacyIdentifiers.map(code => ({system: '', code}));
  }

  delete (merged as {locationTypeCodes?: unknown}).locationTypeCodes;
  return merged;
}

function asStringList(value: unknown): string[] | undefined {
  return Array.isArray(value) && value.every(entry => typeof entry === 'string')
    ? (value as string[])
    : undefined;
}
