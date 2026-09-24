import type {FacilityDraft} from '../onboarding/types';
import type {Operation} from './http';
import type {
  Acknowledgement,
  AvailableMeasure,
  CensusListKey,
  CensusListResult,
  CommitResult,
  ConnectionResult,
  EncounterCode,
  EncounterCodeDetail,
  EncounterMapping,
  FhirConfig,
  HslocCode,
  HslocMapping,
  ImportResult,
  LocationCandidate,
  LocationMethod,
  Measure,
  MrnIntake,
  MrnIntakeOptions,
  Paged,
  PageRequest,
  PatientIdentifier,
  PatientPipeline,
  PreQualIssue,
  QueryPlan,
  AcquisitionLogEntry,
  ReportDetail,
  ReportPatientEntry,
  PatientMappingEvidence,
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
  /**
   * The vendor's import package — a zip of the import sheet and its instruction documents,
   * assembled server-side from the facility's vendor. Rejects until a vendor is chosen.
   */
  exportDraft(): Promise<Blob>;
  completeOnboarding(): Promise<CommitResult>;
  getCommitState(): Promise<CommitResult | null>;

  // reference data — drives vendor branching, so no step names a vendor
  getVendorProfiles(): Promise<VendorProfile[]>;
  getTimezones(): Promise<Timezone[]>;
  getMeasures(): Promise<Measure[]>;
  getHslocCodes(): Promise<HslocCode[]>;
  getEncounterCodes(query?: string): Promise<EncounterCode[]>;
  lookupEncounterCode(system: string, code: string): Promise<EncounterCodeDetail | null>;
  /** Authenticated fetch — a plain <a href> will not work. Key from the vendor profile. */
  getDocument(documentKey: string): Promise<Blob>;

  // fhir server
  testFhirConnection(config: FhirConfig): Promise<ConnectionResult>;

  // patients of interest
  queryPatientList(key: CensusListKey): Promise<CensusListResult>;
  /** All six lists in one call. Preferred over calling queryPatientList six times. */
  queryPatientLists(): Promise<CensusListResult[]>;
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
  /** The step's checkbox option sets — same for every facility, so callers may cache it. */
  getMrnIntakeOptions(): Promise<MrnIntakeOptions>;

  // reporting
  requestReport(request: ReportRequest): Promise<Operation<ReportSummary>>;
  // Each item carries its measure mapping too (ReportDetail, not just ReportSummary), so the
  // list can resolve friendly measure names the same way the detail view does.
  listReports(page: PageRequest): Promise<Paged<ReportDetail>>;
  getReport(reportId: string): Promise<ReportDetail>;
  getReportPatients(reportId: string): Promise<ReportPatientEntry[]>;
  getPatientMappingEvidence(reportId: string, patientId: string): Promise<PatientMappingEvidence>;
  getPatientPreQualResults(reportId: string, patientId: string): Promise<PreQualIssue[]>;
  getPatientStatuses(reportId: string): Promise<PatientPipeline[]>;
  getQueryPlan(reportId: string): Promise<QueryPlan>;
  getAcquisitionLogs(reportId: string): Promise<AcquisitionLogEntry[]>;
  exportReportSummary(reportId: string): Promise<Blob>;
  exportPatientReport(reportId: string, patientId: string, reportType: string): Promise<Blob>;
  /** The most recently recorded ReportAccuracy acknowledgement for this report, or null if none yet. */
  getReportAcknowledgement(reportId: string): Promise<boolean | null>;
  acknowledgeReport(reportId: string, acknowledgement: Acknowledgement): Promise<void>;

  // reporting plan
  getReportingPlan(): Promise<ReportingPlan>;
  /**
   * The current facility's reporting-plan measures MeasureEval can actually evaluate. Read fresh
   * by both the Reporting Plan step (its completion gate) and the Generate Test Report step (its
   * measure picker) — neither caches it, so neither goes stale relative to the other.
   */
  getAvailableMeasures(): Promise<AvailableMeasure[]>;

  // vendor instruction PDFs — served from the BFF's anonymous static-asset routes, not `getDocument`
  getJwksInstructionsPdf(vendor: string): Promise<Blob>;
  getLocationOrgResolutionPdf(): Promise<Blob>;
  getCensusInstructionsPdf(vendor: string): Promise<Blob>;
}
