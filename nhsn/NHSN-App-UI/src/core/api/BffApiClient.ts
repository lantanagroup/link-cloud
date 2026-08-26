import type {ApiClient, DraftEnvelope} from './ApiClient';
import type {FacilityDraft} from '../onboarding/types';
import {HttpClient, pollOperation, type Operation} from './http';
import type {
  Acknowledgement,
  AcquisitionLogEntry,
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
  ReportDetail,
  ReportRequest,
  ReportSummary,
  ReportingPlan,
  SftpConfig,
  SftpCredentials,
  SftpFile,
  SftpFilePreview,
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

  constructor(apiBaseUrl = '/api') {
    this.http = new HttpClient(`${normalizeBase(apiBaseUrl)}/nhsn-app-bff`);
  }

  // ------------------------------------------------------------ session

  async getUserInfo(): Promise<UserInfoResponse> {
    const {data} = await this.http.get<UserInfoResponse>('/userinfo');
    return data;
  }

  // ------------------------------------------------------------ draft

  async getDraft(): Promise<DraftEnvelope> {
    const {data, etag} = await this.http.get<Omit<DraftEnvelope, 'etag'>>('/onboarding');
    return {...data, etag};
  }

  async saveDraft(draft: FacilityDraft, etag?: string): Promise<DraftEnvelope> {
    const result = await this.http.put<Omit<DraftEnvelope, 'etag'>>('/onboarding', draft, {
      ifMatch: etag
    });
    return {...result.data, etag: result.etag};
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

  async getVendorProfiles(): Promise<VendorProfile[]> {
    const {data} = await this.http.get<VendorProfile[]>('/reference/vendors');
    return data;
  }

  async getTimezones(): Promise<Timezone[]> {
    const {data} = await this.http.get<Timezone[]>('/reference/timezones');
    return data;
  }

  async getMeasures(): Promise<Measure[]> {
    const {data} = await this.http.get<Measure[]>('/reference/measures');
    return data;
  }

  async getHslocCodes(): Promise<HslocCode[]> {
    const {data} = await this.http.get<HslocCode[]>('/reference/hsloc-codes');
    return data;
  }

  async getEncounterCodes(query: string): Promise<EncounterCode[]> {
    const {data} = await this.http.get<EncounterCode[]>(
      `/reference/encounter-codes?q=${encodeURIComponent(query)}`
    );
    return data;
  }

  async getDocument(documentKey: string): Promise<Blob> {
    const {data} = await this.http.get<Blob>(`/documents/${encodeURIComponent(documentKey)}`, {
      responseType: 'blob'
    });
    return data;
  }

  // ------------------------------------------------------------ fhir server

  async testFhirConnection(config: FhirConfig): Promise<ConnectionResult> {
    const {data} = await this.http.post<ConnectionResult>('/fhir-server/connection-tests', config);
    return data;
  }

  // ------------------------------------------------------------ patients of interest

  async queryPatientList(key: CensusListKey): Promise<CensusListResult> {
    const {data} = await this.http.post<CensusListResult>('/patients-of-interest/list-queries', {
      listKey: key
    });
    return data;
  }

  async listSftpFiles(): Promise<SftpFile[]> {
    const {data} = await this.http.get<SftpFile[]>('/patients-of-interest/sftp-files');
    return data;
  }

  async previewSftpFile(fileId: string): Promise<SftpFilePreview> {
    const {data} = await this.http.post<SftpFilePreview>(
      `/patients-of-interest/sftp-files/${encodeURIComponent(fileId)}/previews`
    );
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

  // ------------------------------------------------------------ reporting

  async requestReport(request: ReportRequest): Promise<Operation<ReportSummary>> {
    const initial = await this.http.post<ReportSummary>('/reports', request);
    return pollOperation(this.http, initial, {isDone: isReportSettled});
  }

  async listReports(page: PageRequest): Promise<Paged<ReportSummary>> {
    const {data} = await this.http.get<Paged<ReportSummary>>(
      `/reports?page=${page.page}&pageSize=${page.pageSize}`
    );
    return data;
  }

  async getReport(reportId: string): Promise<ReportDetail> {
    const {data} = await this.http.get<ReportDetail>(`/reports/${encodeURIComponent(reportId)}`);
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

  async regenerateReport(reportId: string): Promise<Operation<ReportSummary>> {
    const initial = await this.http.post<ReportSummary>(
      `/reports/${encodeURIComponent(reportId)}/regenerations`
    );
    return pollOperation(this.http, initial, {isDone: isReportSettled});
  }

  async acknowledgeReport(reportId: string, acknowledgement: Acknowledgement): Promise<void> {
    await this.http.put<void>(
      `/reports/${encodeURIComponent(reportId)}/acknowledgement`,
      acknowledgement
    );
  }

  // ------------------------------------------------------------ reporting plan

  async getReportingPlan(): Promise<ReportingPlan> {
    const {data} = await this.http.get<ReportingPlan>('/reporting-plan');
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
