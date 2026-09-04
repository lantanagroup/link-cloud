/**
 * Request/response types for the NHSN-App-BFF endpoints.
 *
 * Property casing follows what the BFF actually serializes. The endpoints that
 * exist today (`/userinfo`, `/facilities/{id}/onboarding`) return PascalCase;
 * the onboarding endpoints added for this flow return camelCase. Do not
 * "tidy" either one — these mirror the wire format.
 */

// ---------------------------------------------------------------- session

export type AccessState = 'Allowed' | 'MissingFacility' | 'MissingRequiredRole';

/** Where one section of the assembled draft came from, and whether it arrived. */
export interface SectionSource {
  section: string;
  /** "Tenant", "DataAcquisition", "Census" or "Bff". */
  origin: string;
  status: SectionStatus;
  traceId?: string;
  detail?: string;
}

export type SectionStatus = 'Ok' | 'Unavailable';

export type EhrVendor = 'Epic' | 'Cerner';

export type OnboardingStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Committing'
  | 'Complete'
  | 'CommitFailed';

/**
 * Backend capabilities that do not exist yet. The BFF serves fixtures behind
 * these flags and reports the resolved set here, so a step can render an
 * honest "not connected to live data" state instead of presenting a fixture
 * as facility data.
 */
export interface Capabilities {
  patientListWithNames: boolean;
  fhirConnectionProbe: boolean;
  sftpFileListing: boolean;
}

export interface UserInfoResponse {
  accessState: AccessState;
  email: string;
  name: string;
  isFacilityAdmin: boolean;
  isOnboarded: boolean;
  hasFacility: boolean;
  facilityId?: string;
  facilityName?: string;
  groups: string[];
  availableNavigation: string[];
  accessRequestUrl?: string;
  vendor?: EhrVendor;
  onboardingStatus?: OnboardingStatus;
  currentStepId?: string;
  capabilities?: Capabilities;
}

export interface FacilitySummaryResponse {
  id: string;
  facilityId: string;
  isOnboarded: boolean;
}

export interface FhirServerInfoResponse {
  fhirServerBaseUrl?: string;
  maxConcurrentRequests?: number;
  maxRetries?: number;
  /** HH:MM, facility-local. */
  minAcquisitionPullTime?: string;
  maxAcquisitionPullTime?: string;
  lagDays?: number;
  lagHours?: number;
  lagMinutes?: number;
}

// ---------------------------------------------------------------- reference data

export type LocationMethod =
  | 'managing-org'
  | 'location-identifier'
  | 'location-type'
  | 'custom-fhir-path';

export type CensusAcquisition = 'PatientList' | 'Sftp';

/**
 * Everything that differs between Epic and Cerner, served as data so no step
 * component contains a vendor name. Adding a vendor is a backend seed row.
 */
export interface VendorProfile {
  vendor: EhrVendor;
  displayName: string;
  censusAcquisition: CensusAcquisition;
  /** Which patient-list keys this vendor expects, empty when it uses sFTP. */
  patientListKeys: CensusListKey[];
  locationMethods: LocationMethod[];
  /** Keys for `getDocument` — never a filename. */
  documentKeys: {
    censusInstructions?: string;
    jwksInstructions?: string;
    locationOrgResolution?: string;
  };
  /** Column label for the vendor's own location code in the HSLOC table. */
  hslocSourceLabel: string;
}

export interface Timezone {
  id: string;
  displayName: string;
}

export interface Measure {
  id: string;
  name: string;
}

export interface HslocCode {
  code: string;
  display: string;
}

export interface EncounterCode {
  system: string;
  code: string;
  display: string;
  category?: string;
  categoryName?: string;
}

// ---------------------------------------------------------------- fhir server

export interface FhirConfig {
  fhirServerBaseUrl: string;
  maxConcurrentRequests?: number;
  maxRetries?: number;
  /** ISO 8601 durations, e.g. `PT30M`. */
  minAcquisitionPullTime?: string;
  maxAcquisitionPullTime?: string;
  lagDuration?: string;
}

export interface ConnectionResult {
  success: boolean;
  messageKey: string;
  detail?: string;
  /** True when answered by a fixture adapter rather than a live service. */
  simulated?: boolean;
}

// ---------------------------------------------------------------- patients of interest

export type CensusListKey =
  | 'admit-lt-24'
  | 'admit-24-to-48'
  | 'admit-gt-48'
  | 'discharge-lt-24'
  | 'discharge-24-to-48'
  | 'discharge-gt-48';

export interface CensusListResult {
  listKey: CensusListKey;
  patientCount: number;
  patientIds: string[];
  simulated?: boolean;
}

/** Identified by fileName - the endpoint has no file id. Patients arrive attached, no separate preview call. */
export interface SftpFile {
  fileName: string;
  queriedAt: string;
  patientIds: string[];
  simulated?: boolean;
}

export interface SftpConfig {
  host: string;
  port: number;
  remoteDirectory: string;
  removeAfterProcessing: boolean;
}

/** Write-only. Never returned by a read, never stored in the draft. */
export interface SftpCredentials {
  username: string;
  password: string;
}

export type AcknowledgementKind = 'CensusAccuracy' | 'ReportAccuracy';

export interface Acknowledgement {
  kind: AcknowledgementKind;
  accepted: boolean;
  statementKey: string;
}

// ---------------------------------------------------------------- mapping steps

/** One coding of a Location.type CodeableConcept - a Location commonly carries more than one. */
export interface LocationTypeCoding {
  system?: string;
  code?: string;
  display?: string;
}

export interface LocationCandidate {
  id: string;
  display: string;
  system?: string;
  code?: string;
  typeText?: string;
  typeCodings?: LocationTypeCoding[];
}

export interface HslocMapping {
  sourceCode: string;
  sourceDisplay?: string;
  hslocCode: string;
}

export interface EncounterMapping {
  system: string;
  code: string;
  display?: string;
  encounterType: string;
}

// ---------------------------------------------------------------- mrn intake

export interface MrnIdentifierRule {
  ordinal: number;
  element: string;
  rule: string;
  value: string;
}

export interface PatientIdentifier {
  patientId: string;
  elements: Array<{ system?: string; value: string; type?: string }>;
}

export interface MrnIntake {
  hasMultipleMrn: boolean;
  multipleMrnTypes: string[];
  multipleMrnOtherText?: string;
  userFacingIdentifierNames: string[];
  isCalledMrn: boolean;
  otherTermUsed?: string;
  canSearchByIdentifier: boolean;
  variesByFacility: boolean;
  varianceTypes: string[];
  varianceOtherText?: string;
  changesOverTime: boolean;
  changeTypes: string[];
  changeOtherText?: string;
  rules: MrnIdentifierRule[];
  observations: Array<{ patientId: string; elements: string[] }>;
}

// ---------------------------------------------------------------- reporting

export interface ReportRequest {
  measures: string[];
  startDate: string;
  endDate: string;
  patientIds: string[];
}

export type ReportStatus = 'Pending' | 'Complete' | 'Failed' | 'Cancelled';

export interface ReportSummary {
  reportId: string;
  measures: string[];
  patientCount: number;
  startDate: string;
  endDate: string;
  createDate: string;
  status: ReportStatus;
  regeneratedFrom?: string;
}

export interface ReportDetail extends ReportSummary {
  measureMapping: Array<{ nhsnMeasure: string; digitalQualityMeasure: string }>;
}

/**
 * Link exposes five reporting statuses; this view renders an eleven-node
 * pipeline. The BFF owns that projection — the UI renders the nodes it is
 * given and never maps a Link enum itself (see §7 of the BFF plan).
 */
export type PipelineNodeState = 'complete' | 'current' | 'pending';

export interface PatientPipeline {
  patientId: string;
  status: string;
  currentNode: string;
  nodes: Array<{ id: string; state: PipelineNodeState }>;
  resourceCount?: number;
}

export interface QueryPlan {
  reportId: string;
  planJson: string;
}

export interface AcquisitionLogEntry {
  timestamp: string;
  level: string;
  message: string;
}

// ---------------------------------------------------------------- reporting plan

export interface ReportingPlanRow {
  month: string;
  year: number;
  measures: string[];
}

export interface ReportingPlan {
  rows: ReportingPlanRow[];
}

// ---------------------------------------------------------------- commit

export type CommitTargetStatus = 'committed' | 'failed' | 'pending';

export interface CommitResult {
  facilityId: string;
  services: Array<{
    service: string;
    stage: 1 | 2;
    status: CommitTargetStatus;
    detail?: string;
  }>;
}

// ---------------------------------------------------------------- import/export

export interface ImportResult {
  accepted: boolean;
  cellErrors: Array<{ sheet: string; cell: string; messageKey: string }>;
  /** How many recognized fields had a non-empty value in the uploaded sheet. */
  fieldsImported: number;
  /** How many fields the import sheet defines in total. */
  totalFields: number;
}

// ---------------------------------------------------------------- paging

export interface PageRequest {
  page: number;
  pageSize: number;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
