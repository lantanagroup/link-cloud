import type {ApiClient, DraftEnvelope} from './ApiClient';
import type {FacilityDraft} from '../onboarding/types';
import {HttpClient, pollOperation, type Operation} from './http';
import {cachedReference} from './referenceCache';
import type {
  Acknowledgement,
  AcquisitionLogEntry,
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
  UserInfoResponse,
  VendorProfile
} from './contracts';

/**
 * The real client.
 *
 * Owns endpoint paths below `apiBaseUrl` and nothing above it: the `/api`
 * segment comes from the host via the `apibaseurl` attribute, so if the NHSN
 * gateway mounts us elsewhere only the attribute changes and no code does.
 *
 * Sends no Authorization header — see the comment in http.ts.
 */
export class BffApiClient implements ApiClient {
  private readonly http: HttpClient;
  private readonly bffBaseUrl: string;

  constructor(apiBaseUrl = '/api') {
    this.bffBaseUrl = `${normalizeBase(apiBaseUrl)}/nhsn-app-bff`;
    this.http = new HttpClient(this.bffBaseUrl);
  }

  // ------------------------------------------------------------ session

  async getUserInfo(): Promise<UserInfoResponse> {
    const {data} = await this.http.get<UserInfoResponse>('/userinfo');
    return data;
  }

  // ------------------------------------------------------------ draft

  async getDraft(): Promise<DraftEnvelope> {
    const {data} = await this.http.get<DraftEnvelope>('/onboarding');
    return data;
  }

  // Saving the draft can trigger synchronous downstream work (e.g. re-validating patients-of-
  // interest configuration) that occasionally runs past the client's default 30s budget on a slow
  // connection - give it more room than a plain field save needs before giving up.
  async saveDraft(draft: FacilityDraft): Promise<DraftEnvelope> {
    const {data} = await this.http.put<DraftEnvelope>('/onboarding', draft, {timeoutMs: 60_000});
    return data;
  }

  async importDraft(file: File): Promise<ImportResult> {
    const form = new FormData();
    form.append('file', file);
    const {data} = await this.http.post<ImportResult>('/onboarding/import', form);
    return data;
  }

  async exportDraft(): Promise<Blob> {
    const {data} = await this.http.get<Blob>('/onboarding/export', {responseType: 'blob'});
    return data;
  }

  async completeOnboarding(): Promise<CommitResult> {
    const {data} = await this.http.post<CommitResult>('/onboarding/completion');
    return data;
  }

  async getCommitState(): Promise<CommitResult | null> {
    const {data} = await this.http.get<CommitResult | null>('/onboarding/completion');
    return data ?? null;
  }

  // ------------------------------------------------------------ reference data

  // These four are static, same bytes for every facility and user, and back
  // dropdowns that several steps mount independently — cachedReference is what
  // keeps walking back and forth across onboarding from refetching each one.
  async getVendorProfiles(): Promise<VendorProfile[]> {
    return cachedReference('vendors', async () => {
      const {data} = await this.http.get<VendorProfile[]>('/reference/vendors');
      return data;
    });
  }

  async getTimezones(): Promise<Timezone[]> {
    return cachedReference('timezones', async () => {
      const {data} = await this.http.get<Timezone[]>('/reference/timezones');
      return data;
    });
  }

  async getMeasures(): Promise<Measure[]> {
    return cachedReference('measures', async () => {
      const {data} = await this.http.get<Measure[]>('/reference/measures');
      return data;
    });
  }

  async getHslocCodes(): Promise<HslocCode[]> {
    return cachedReference('hsloc-codes', async () => {
      const {data} = await this.http.get<HslocCode[]>('/reference/hsloc-codes');
      return data;
    });
  }

  async getEncounterCodes(query?: string): Promise<EncounterCode[]> {
    const q = query?.trim();
    if (!q) {
      return cachedReference('encounter-codes', async () => {
        const {data} = await this.http.get<EncounterCode[]>('/encounter/encounter-codes');
        return data;
      });
    }

    const {data} = await this.http.get<EncounterCode[]>(
      `/encounter/encounter-codes?q=${encodeURIComponent(q)}`
    );
    return data;
  }

  async lookupEncounterCode(system: string, code: string): Promise<EncounterCodeDetail | null> {
    const {data} = await this.http.get<EncounterCodeDetail | null>(
      `/encounter/encounter-codes/lookup?system=${encodeURIComponent(system)}&code=${encodeURIComponent(code)}`
    );
    return data ?? null;
  }

  async getDocument(documentKey: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(`/documents/${encodeURIComponent(documentKey)}`, {
      responseType: 'blob'
    });
    return data;
  }

  // ------------------------------------------------------------ fhir server

  async testFhirConnection(config: FhirConfig): Promise<ConnectionResult> {
    const {data} = await this.http.post<ConnectionResult>('/fhir-server/test-connection', config);
    return data;
  }

  // ------------------------------------------------------------ patients of interest

  async queryPatientList(key: CensusListKey): Promise<CensusListResult> {
    const {data} = await this.http.post<CensusListResult>('/patients-of-interest/list-queries', {
      listKey: key
    });
    return data;
  }

  async queryPatientLists(): Promise<CensusListResult[]> {
    const {data} = await this.http.get<CensusListResult[]>('/patients-of-interest/list-queries');
    return data;
  }

  async listSftpFiles(): Promise<SftpFile[]> {
    const {data} = await this.http.get<SftpFile[]>('/patients-of-interest/sftp-files');
    return data;
  }

  async testSftpConnection(config: SftpConfig): Promise<ConnectionResult> {
    const {data} = await this.http.post<ConnectionResult>(
      '/patients-of-interest/sftp-connection-tests',
      config
    );
    return data;
  }

  async saveSftpCredentials(credentials: SftpCredentials): Promise<void> {
    await this.http.put<void>('/patients-of-interest/sftp-credentials', credentials);
  }

  async acknowledgeCensus(acknowledgement: Acknowledgement): Promise<void> {
    await this.http.put<void>('/patients-of-interest/acknowledgement', acknowledgement);
  }

  // ------------------------------------------------------------ mapping steps

  async getLocationCandidates(method: LocationMethod): Promise<LocationCandidate[]> {
    const {data} = await this.http.get<LocationCandidate[]>(
      `/organization-identification/location-candidates?method=${encodeURIComponent(method)}`
    );
    return data;
  }

  async getHslocMappings(): Promise<HslocMapping[]> {
    const {data} = await this.http.get<HslocMapping[]>('/hsloc-mappings');
    return data;
  }

  async saveHslocMappings(mappings: HslocMapping[]): Promise<void> {
    await this.http.put<void>('/hsloc-mappings', mappings);
  }

  async getEncounterMappings(): Promise<EncounterMapping[]> {
    const {data} = await this.http.get<EncounterMapping[]>('/encounter-mappings');
    return data;
  }

  async saveEncounterMappings(mappings: EncounterMapping[]): Promise<void> {
    await this.http.put<void>('/encounter-mappings', mappings);
  }

  // ------------------------------------------------------------ mrn intake

  async getMrnIntake(): Promise<MrnIntake | null> {
    const {data} = await this.http.get<MrnIntake | null>('/mrn-intake');
    return data ?? null;
  }

  async saveMrnIntake(intake: MrnIntake): Promise<void> {
    await this.http.put<void>('/mrn-intake', intake);
  }

  async getPatientIdentifiers(): Promise<PatientIdentifier[]> {
    const {data} = await this.http.get<PatientIdentifier[]>('/mrn-intake/patient-identifiers');
    return data;
  }

  async getMrnIntakeOptions(): Promise<MrnIntakeOptions> {
    return cachedReference('mrn-intake-options', async () => {
      const {data} = await this.http.get<MrnIntakeOptions>('/mrn-intake/options');
      return data;
    });
  }

  // ------------------------------------------------------------ reporting

  async requestReport(request: ReportRequest): Promise<Operation<ReportSummary>> {
    const initial = await this.http.post<ReportSummary>('/reports', request);
    return pollOperation(this.http, initial, {isDone: isReportSettled});
  }

  async listReports(page: PageRequest): Promise<Paged<ReportDetail>> {
    const {data} = await this.http.get<Paged<ReportDetail>>(
      `/reports?page=${page.page}&pageSize=${page.pageSize}`
    );
    return data;
  }

  async getReport(reportId: string): Promise<ReportDetail> {
    const {data} = await this.http.get<ReportDetail>(`/reports/${encodeURIComponent(reportId)}`);
    return data;
  }

  async getReportPatients(reportId: string): Promise<ReportPatientEntry[]> {
    const {data} = await this.http.get<ReportPatientEntry[]>(
      `/reports/${encodeURIComponent(reportId)}/patients`
    );
    return data;
  }

  async getPatientMappingEvidence(reportId: string, patientId: string): Promise<PatientMappingEvidence> {
    const {data} = await this.http.get<PatientMappingEvidence>(
      `/reports/${encodeURIComponent(reportId)}/patients/${encodeURIComponent(patientId)}/mapping-evidence`
    );
    return data;
  }

  async getPatientPreQualResults(reportId: string, patientId: string): Promise<PreQualIssue[]> {
    const {data} = await this.http.get<PreQualIssue[]>(
      `/reports/${encodeURIComponent(reportId)}/patients/${encodeURIComponent(patientId)}/pre-qual-results`
    );
    return data;
  }

  async getPatientStatuses(reportId: string): Promise<PatientPipeline[]> {
    const {data} = await this.http.get<PatientPipeline[]>(
      `/reports/${encodeURIComponent(reportId)}/patient-statuses`
    );
    return data;
  }

  async getQueryPlan(reportId: string): Promise<QueryPlan> {
    const {data} = await this.http.get<QueryPlan>(
      `/reports/${encodeURIComponent(reportId)}/query-plan`
    );
    return data;
  }

  async getAcquisitionLogs(reportId: string): Promise<AcquisitionLogEntry[]> {
    const {data} = await this.http.get<AcquisitionLogEntry[]>(
      `/reports/${encodeURIComponent(reportId)}/acquisition-logs`
    );
    return data;
  }

  async exportReportSummary(reportId: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(
      `/reports/${encodeURIComponent(reportId)}/summary-export`,
      {responseType: 'blob'}
    );
    return data;
  }

  async exportPatientReport(reportId: string, patientId: string, reportType: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(
      `/reports/${encodeURIComponent(reportId)}/patients/${encodeURIComponent(patientId)}/export?measure=${encodeURIComponent(reportType)}`,
      {responseType: 'blob'}
    );
    return data;
  }

  async getReportAcknowledgement(reportId: string): Promise<boolean | null> {
    const {data} = await this.http.get<{accepted: boolean | null}>(`/reports/${encodeURIComponent(reportId)}/acknowledgement`);
    return data.accepted;
  }

  async acknowledgeReport(reportId: string, acknowledgement: Acknowledgement): Promise<void> {
    await this.http.put<void>(`/reports/${encodeURIComponent(reportId)}/acknowledgement`, acknowledgement);
  }

  // ------------------------------------------------------------ reporting plan

  async getReportingPlan(): Promise<ReportingPlan> {
    const {data} = await this.http.get<ReportingPlan>('/reporting-plan');
    return data;
  }

  async getAvailableMeasures(): Promise<AvailableMeasure[]> {
    const {data} = await this.http.get<AvailableMeasure[]>('/reporting-plan/measures');
    return data;
  }

  // ------------------------------------------------------------ vendor instruction PDFs

  async getJwksInstructionsPdf(vendor: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(`/static/jwks-instructions/${encodeURIComponent(vendor)}`, {
      responseType: 'blob'
    });
    return data;
  }

  async getLocationOrgResolutionPdf(): Promise<Blob> {
    const {data} = await this.http.get<Blob>('/static/location-org-resolution', {responseType: 'blob'});
    return data;
  }

  async getCensusInstructionsPdf(vendor: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(`/static/census-instructions/${encodeURIComponent(vendor)}`, {
      responseType: 'blob'
    });
    return data;
  }
}

function isReportSettled(value: unknown): boolean {
  const status = (value as ReportSummary | undefined)?.status;
  return status !== undefined && status !== 'Pending';
}

function normalizeBase(value: string): string {
  const trimmed = value.trim();
  if (!trimmed || trimmed === '/') {
    return '';
  }
  return trimmed.endsWith('/') ? trimmed.slice(0, -1) : trimmed;
}
