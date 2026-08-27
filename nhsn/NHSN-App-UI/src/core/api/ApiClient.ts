import type {FacilityDraft} from '../onboarding/types';
import type {Operation} from './http';
import type {
  Acknowledgement,
  CensusListKey,
  CensusListResult,
  CommitResult,
  ConnectionResult,
  EncounterCode,
  EncounterMapping,
  FhirConfig,
  HslocCode,
  HslocMapping,
  ImportResult,
  LocationCandidate,
  LocationMethod,
  Measure,
  MrnIntake,
  Paged,
  PageRequest,
  PatientIdentifier,
  PatientPipeline,
  QueryPlan,
  AcquisitionLogEntry,
  ReportDetail,
  ReportRequest,
  ReportSummary,
  ReportingPlan,
  SftpConfig,
  SftpCredentials,
  SftpFile,
  Timezone,
  SectionSource,
  UserInfoResponse,
  VendorProfile
} from './contracts';

export interface DraftEnvelope {
  draft: FacilityDraft | null;
  commitState: CommitResult | null;
  /** Per-section origin and status. `Unavailable` means unreachable, not "read fine and empty". */
  sources: SectionSource[];
}

/**
 * The port every step talks to.
 *
 * No method takes a facility. Facility is resolved server-side from the
 * `facility` claim and the BFF's onboarding routes carry no facility segment —
 * a facility the client cannot name is a facility the client cannot spoof.
 *
 * Expressed in draft terms, not HTTP terms: no token, no service URLs, no
 * status codes. Steps never learn the backend topology.
 */
export interface ApiClient {
  // session
  getUserInfo(): Promise<UserInfoResponse>;

  // draft
  getDraft(): Promise<DraftEnvelope>;
  /**
   * Saves the draft. The BFF writes only the section for `draft.currentStepId`, sent *before* the
   * transition is applied — send it after and that step's values go unsaved. No ETag: a stale tab
   * can only overwrite its own step.
   */
  saveDraft(draft: FacilityDraft): Promise<DraftEnvelope>;
  importDraft(file: File): Promise<ImportResult>;
  exportDraft(): Promise<Blob>;
  completeOnboarding(): Promise<CommitResult>;
  getCommitState(): Promise<CommitResult | null>;

  // reference data — drives vendor branching, so no step names a vendor
  getVendorProfiles(): Promise<VendorProfile[]>;
  getTimezones(): Promise<Timezone[]>;
  getMeasures(): Promise<Measure[]>;
  getHslocCodes(): Promise<HslocCode[]>;
  getEncounterCodes(query: string): Promise<EncounterCode[]>;
  /** Authenticated fetch — a plain <a href> will not work. Key from the vendor profile. */
  getDocument(documentKey: string): Promise<Blob>;

  // fhir server
  testFhirConnection(config: FhirConfig): Promise<ConnectionResult>;

  // patients of interest
  queryPatientList(key: CensusListKey): Promise<CensusListResult>;
  listSftpFiles(): Promise<SftpFile[]>;
  testSftpConnection(config: SftpConfig): Promise<ConnectionResult>;
  /** Write-only: forwarded to Data Acquisition, never persisted here, never read back. */
  saveSftpCredentials(credentials: SftpCredentials): Promise<void>;
  acknowledgeCensus(acknowledgement: Acknowledgement): Promise<void>;

  // mapping steps
  getLocationCandidates(method: LocationMethod): Promise<LocationCandidate[]>;
  getHslocMappings(): Promise<HslocMapping[]>;
  saveHslocMappings(mappings: HslocMapping[]): Promise<void>;
  getEncounterMappings(): Promise<EncounterMapping[]>;
  saveEncounterMappings(mappings: EncounterMapping[]): Promise<void>;

  // mrn intake — normalized server-side, mirrored into the draft
  getMrnIntake(): Promise<MrnIntake | null>;
  saveMrnIntake(intake: MrnIntake): Promise<void>;
  getPatientIdentifiers(): Promise<PatientIdentifier[]>;

  // reporting
  requestReport(request: ReportRequest): Promise<Operation<ReportSummary>>;
  listReports(page: PageRequest): Promise<Paged<ReportSummary>>;
  getReport(reportId: string): Promise<ReportDetail>;
  getPatientStatuses(reportId: string): Promise<PatientPipeline[]>;
  getQueryPlan(reportId: string): Promise<QueryPlan>;
  getAcquisitionLogs(reportId: string): Promise<AcquisitionLogEntry[]>;
  exportReportSummary(reportId: string): Promise<Blob>;
  regenerateReport(reportId: string): Promise<Operation<ReportSummary>>;
  acknowledgeReport(reportId: string, acknowledgement: Acknowledgement): Promise<void>;

  // reporting plan
  getReportingPlan(): Promise<ReportingPlan>;
}
