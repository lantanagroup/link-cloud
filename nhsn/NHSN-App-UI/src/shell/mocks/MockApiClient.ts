import type {ApiClient, DraftEnvelope} from '../../core/api/ApiClient';
import type {FacilityDraft} from '../../core/onboarding/types';
import {createEmptyDraft, migrateDraft} from '../../core/onboarding/types';
import type {Operation} from '../../core/api/http';
import type * as C from '../../core/api/contracts';

/**
 * Offline implementation of the port.
 *
 * This is not a convenience. The BFF's onboarding endpoints do not exist yet,
 * so until its phase 1 lands this is the only way any step runs — every step
 * story is developed against it. It therefore has to track the real contracts
 * rather than whatever is easiest to fake, or thirteen stories get built
 * against a fiction.
 *
 * Everything it invents is marked `simulated: true` and uses obviously
 * synthetic values, so a lower-environment screenshot can never be mistaken
 * for real facility data.
 */
const DRAFT_KEY_PREFIX = 'nhsn-app-ui.mockDraft.';
const LATENCY_MS = 120;

// Every section healthy - mock mode reads from localStorage, so nothing can be unavailable.
// Build a step's Unavailable state against the real BFF with a service stopped, not against this.
const MOCK_SOURCES: C.SectionSource[] = [
  {section: 'workflow', origin: 'Bff', status: 'Ok'},
  {section: 'facilityInfo', origin: 'Tenant', status: 'Ok'},
  {section: 'fhir', origin: 'DataAcquisition', status: 'Ok'},
  {section: 'census', origin: 'Census', status: 'Ok'}
];

export class MockApiClient implements ApiClient {
  constructor(
    private readonly facilityId = 'MOCK-FACILITY-001',
    private readonly facilityName = 'Mock Facility'
  ) {}

  private get draftKey(): string {
    return `${DRAFT_KEY_PREFIX}${this.facilityId}`;
  }

  // ------------------------------------------------------------ session

  async getUserInfo(): Promise<C.UserInfoResponse> {
    await tick();
    return {
      accessState: 'Allowed',
      email: 'facility.admin@example.invalid',
      name: 'Sample Facility Admin',
      isFacilityAdmin: true,
      isOnboarded: false,
      hasFacility: true,
      facilityId: this.facilityId,
      facilityName: this.facilityName,
      groups: ['FACADMIN'],
      availableNavigation: ['onboarding'],
      vendor: 'Epic',
      onboardingStatus: 'InProgress',
      // Both off, matching every non-development environment. Steps must
      // render their "not yet connected" state rather than showing fixtures.
      capabilities: {
        patientListWithNames: false,
        fhirConnectionProbe: false
      }
    };
  }

  // ------------------------------------------------------------ draft

  async getDraft(): Promise<DraftEnvelope> {
    await tick();
    const raw = window.localStorage.getItem(this.draftKey);
    return {
      draft: raw ? migrateDraft(JSON.parse(raw)) : createEmptyDraft(),
      commitState: null,
      sources: MOCK_SOURCES
    };
  }

  async saveDraft(draft: FacilityDraft): Promise<DraftEnvelope> {
    await tick();
    // No conflict simulation - the BFF scopes each write to its own step, so there's nothing to simulate.
    window.localStorage.setItem(this.draftKey, JSON.stringify(draft));
    return {draft, commitState: null, sources: MOCK_SOURCES};
  }

  async importDraft(): Promise<C.ImportResult> {
    await tick();
    return {accepted: true, cellErrors: [], fieldsImported: 0, totalFields: 17};
  }

  async exportDraft(): Promise<Blob> {
    await tick();
    return new Blob(['simulated import sheet'], {type: 'text/plain'});
  }

  async completeOnboarding(): Promise<C.CommitResult> {
    await tick();
    return {
      facilityId: this.facilityId,
      services: [
        {service: 'Tenant', stage: 1, status: 'committed'},
        {service: 'DataAcquisition', stage: 1, status: 'committed'},
        {service: 'Census', stage: 2, status: 'committed'},
        {service: 'QueryDispatch', stage: 2, status: 'committed'},
        {service: 'Report', stage: 2, status: 'committed'}
      ]
    };
  }

  async getCommitState(): Promise<C.CommitResult | null> {
    await tick();
    return null;
  }

  // ------------------------------------------------------------ reference data

  async getVendorProfiles(): Promise<C.VendorProfile[]> {
    await tick();
    return [
      {
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
        locationMethods: ['managing-org', 'location-identifier', 'custom-fhir-path'],
        documentKeys: {
          censusInstructions: 'epic-census-instructions',
          jwksInstructions: 'epic-jwks-instructions',
          locationOrgResolution: 'location-org-resolution'
        },
        hslocSourceLabel: 'Epic Location Code'
      },
      {
        vendor: 'Cerner',
        displayName: 'Cerner',
        censusAcquisition: 'Sftp',
        patientListKeys: [],
        locationMethods: [
          'managing-org',
          'location-identifier',
          'location-type',
          'custom-fhir-path'
        ],
        documentKeys: {
          censusInstructions: 'cerner-census-instructions',
          jwksInstructions: 'cerner-jwks-instructions',
          locationOrgResolution: 'location-org-resolution'
        },
        hslocSourceLabel: 'Cerner Location Code'
      }
    ];
  }

  async getTimezones(): Promise<C.Timezone[]> {
    await tick();
    return [
      {id: 'America/New_York', displayName: 'America/New_York — Eastern Time'},
      {id: 'America/Detroit', displayName: 'America/Detroit — Eastern Time'},
      {id: 'America/Kentucky/Louisville', displayName: 'America/Kentucky/Louisville — Eastern Time'},
      {id: 'America/Kentucky/Monticello', displayName: 'America/Kentucky/Monticello — Eastern Time'},
      {id: 'America/Indiana/Indianapolis', displayName: 'America/Indiana/Indianapolis — Eastern Time'},
      {id: 'America/Indiana/Vincennes', displayName: 'America/Indiana/Vincennes — Eastern Time'},
      {id: 'America/Indiana/Winamac', displayName: 'America/Indiana/Winamac — Eastern Time'},
      {id: 'America/Indiana/Marengo', displayName: 'America/Indiana/Marengo — Eastern Time'},
      {id: 'America/Indiana/Petersburg', displayName: 'America/Indiana/Petersburg — Eastern Time'},
      {id: 'America/Indiana/Vevay', displayName: 'America/Indiana/Vevay — Eastern Time'},
      {id: 'America/Indiana/Tell_City', displayName: 'America/Indiana/Tell_City — Central Time'},
      {id: 'America/Indiana/Knox', displayName: 'America/Indiana/Knox — Central Time'},
      {id: 'America/Chicago', displayName: 'America/Chicago — Central Time'},
      {id: 'America/Menominee', displayName: 'America/Menominee — Central Time'},
      {id: 'America/North_Dakota/Center', displayName: 'America/North_Dakota/Center — Central Time'},
      {id: 'America/North_Dakota/New_Salem', displayName: 'America/North_Dakota/New_Salem — Central Time'},
      {id: 'America/North_Dakota/Beulah', displayName: 'America/North_Dakota/Beulah — Central Time'},
      {id: 'America/Denver', displayName: 'America/Denver — Mountain Time'},
      {id: 'America/Boise', displayName: 'America/Boise — Mountain Time'},
      {id: 'America/Phoenix', displayName: 'America/Phoenix — Mountain Time (no DST)'},
      {id: 'America/Los_Angeles', displayName: 'America/Los_Angeles — Pacific Time'},
      {id: 'America/Anchorage', displayName: 'America/Anchorage — Alaska Time'},
      {id: 'America/Juneau', displayName: 'America/Juneau — Alaska Time'},
      {id: 'America/Sitka', displayName: 'America/Sitka — Alaska Time'},
      {id: 'America/Metlakatla', displayName: 'America/Metlakatla — Alaska Time'},
      {id: 'America/Yakutat', displayName: 'America/Yakutat — Alaska Time'},
      {id: 'America/Nome', displayName: 'America/Nome — Alaska Time'},
      {id: 'America/Adak', displayName: 'America/Adak — Hawaii-Aleutian Time'},
      {id: 'Pacific/Honolulu', displayName: 'Pacific/Honolulu — Hawaii Time (no DST)'},
      {id: 'America/Puerto_Rico', displayName: 'America/Puerto_Rico — Atlantic Time (Puerto Rico / US Virgin Islands)'},
      {id: 'Pacific/Guam', displayName: 'Pacific/Guam — Chamorro Time (Guam)'},
      {id: 'Pacific/Saipan', displayName: 'Pacific/Saipan — Chamorro Time (N. Mariana Islands)'},
      {id: 'Pacific/Pago_Pago', displayName: 'Pacific/Pago_Pago — Samoa Time (American Samoa)'}
    ];
  }

  async getMeasures(): Promise<C.Measure[]> {
    await tick();
    return [
      {id: 'SAMPLE-MEASURE-1', name: 'Sample Measure One (simulated)'},
      {id: 'SAMPLE-MEASURE-2', name: 'Sample Measure Two (simulated)'}
    ];
  }

  async getHslocCodes(): Promise<C.HslocCode[]> {
    await tick();
    return [
      {code: '1027-2', display: 'Medical Ward (simulated)'},
      {code: '1028-0', display: 'Surgical Ward (simulated)'}
    ];
  }

  async getEncounterCodes(): Promise<C.EncounterCode[]> {
    await tick();
    return [{system: 'http://example.invalid/encounter', code: 'IMP', display: 'Inpatient'}];
  }

  async getDocument(documentKey: string): Promise<Blob> {
    await tick();
    return new Blob([`simulated document: ${documentKey}`], {type: 'text/plain'});
  }

  // ------------------------------------------------------------ capability-gated

  async testFhirConnection(config: C.FhirConfig): Promise<C.ConnectionResult> {
    await tick();
    if (!isValidHttpUrl(config.fhirServerBaseUrl)) {
      return {success: false, messageKey: 'fhirServerInfo.messages.invalidBaseUrl', simulated: true};
    }
    return {success: true, messageKey: 'fhirServerInfo.messages.testSuccess', simulated: true};
  }

  async queryPatientList(key: C.CensusListKey): Promise<C.CensusListResult> {
    await tick();
    return {listKey: key, patientCount: 3, patientIds: ids(3), simulated: true};
  }

  async listSftpFiles(): Promise<C.SftpFile[]> {
    await tick();
    return [
      {
        fileName: 'census-simulated-0001.csv',
        queriedAt: '2026-01-01T00:00:00Z',
        patients: [{patientId: 'SIMULATED-PATIENT-0001', patientName: 'Jane Doe'}],
        simulated: true
      }
    ];
  }

  async testSftpConnection(): Promise<C.ConnectionResult> {
    await tick();
    return {success: true, messageKey: 'census.sftp.simulated', simulated: true};
  }

  async saveSftpCredentials(): Promise<void> {
    await tick();
    // Intentionally stores nothing — credentials are write-only everywhere.
  }

  async acknowledgeCensus(): Promise<void> {
    await tick();
  }

  // ------------------------------------------------------------ mapping steps

  async getLocationCandidates(): Promise<C.LocationCandidate[]> {
    await tick();
    return [{id: 'SIMULATED-LOC-1', display: 'Simulated Location 1'}];
  }

  async getHslocMappings(): Promise<C.HslocMapping[]> {
    await tick();
    return [];
  }

  async saveHslocMappings(): Promise<void> {
    await tick();
  }

  async getEncounterMappings(): Promise<C.EncounterMapping[]> {
    await tick();
    return [];
  }

  async saveEncounterMappings(): Promise<void> {
    await tick();
  }

  // ------------------------------------------------------------ mrn intake

  async getMrnIntake(): Promise<C.MrnIntake | null> {
    await tick();
    return null;
  }

  async saveMrnIntake(): Promise<void> {
    await tick();
  }

  async getPatientIdentifiers(): Promise<C.PatientIdentifier[]> {
    await tick();
    return ids(3).map(patientId => ({
      patientId,
      elements: [{system: 'http://example.invalid/mrn', value: `SIM-${patientId}`, type: 'MR'}]
    }));
  }

  // ------------------------------------------------------------ reporting

  async requestReport(request: C.ReportRequest): Promise<Operation<C.ReportSummary>> {
    const summary = this.buildReport(request);
    return immediate(summary);
  }

  async listReports(page: C.PageRequest): Promise<C.Paged<C.ReportSummary>> {
    await tick();
    return {items: [], page: page.page, pageSize: page.pageSize, totalCount: 0};
  }

  async getReport(reportId: string): Promise<C.ReportDetail> {
    await tick();
    return {
      ...this.buildReport({measures: [], startDate: '', endDate: '', patientIds: []}),
      reportId,
      measureMapping: []
    };
  }

  async getPatientStatuses(): Promise<C.PatientPipeline[]> {
    await tick();
    return ids(3).map(patientId => ({
      patientId,
      status: 'PendingValidation',
      currentNode: 'pending-validation',
      resourceCount: 12,
      nodes: [
        {id: 'initial-acquisition', state: 'complete' as const},
        {id: 'initial-normalization', state: 'complete' as const},
        {id: 'initial-evaluation', state: 'complete' as const},
        {id: 'pending-validation', state: 'current' as const},
        {id: 'passed-validation', state: 'pending' as const}
      ]
    }));
  }

  async getQueryPlan(reportId: string): Promise<C.QueryPlan> {
    await tick();
    return {reportId, planJson: '{ "simulated": true }'};
  }

  async getAcquisitionLogs(): Promise<C.AcquisitionLogEntry[]> {
    await tick();
    return [
      {timestamp: '2026-01-01T00:00:00Z', level: 'Information', message: 'Simulated log entry'}
    ];
  }

  async exportReportSummary(): Promise<Blob> {
    await tick();
    return new Blob(['simulated report summary'], {type: 'text/plain'});
  }

  async regenerateReport(reportId: string): Promise<Operation<C.ReportSummary>> {
    const summary = this.buildReport({measures: [], startDate: '', endDate: '', patientIds: []});
    return immediate({...summary, regeneratedFrom: reportId});
  }

  async acknowledgeReport(): Promise<void> {
    await tick();
  }

  // ------------------------------------------------------------ reporting plan

  async getReportingPlan(): Promise<C.ReportingPlan> {
    await tick();
    return {
      rows: [{month: 'January', year: 2026, measures: ['Sample Measure One (simulated)']}]
    };
  }

  async getFhirServerInfo(): Promise<C.FhirServerInfoResponse> {
    await tick();
    const raw = window.localStorage.getItem(this.draftKey);
    const fhir = (raw ? migrateDraft(JSON.parse(raw)) : createEmptyDraft()).fhir;
    const [lagDays, lagHours, lagMinutes] = parseIso8601Duration(fhir.lagDuration);

    return {
      fhirServerBaseUrl: fhir.fhirServerBaseUrl,
      maxConcurrentRequests: fhir.maxConcurrentRequests,
      maxRetries: fhir.maxRetries,
      minAcquisitionPullTime: fhir.minAcquisitionPullTime,
      maxAcquisitionPullTime: fhir.maxAcquisitionPullTime,
      lagDays,
      lagHours,
      lagMinutes
    };
  }

  getJwksInstructionsUrl(vendor: string): string {
    const body = `Simulated ${vendor} JWKS instructions PDF.\n\nNo backend is connected in mock mode — against the real BFF this downloads the actual instructions PDF.`;
    return URL.createObjectURL(new Blob([body], {type: 'text/plain;charset=utf-8'}));
  }

  private buildReport(request: C.ReportRequest): C.ReportSummary {
    return {
      reportId: 'SIMULATED-REPORT-0001',
      measures: request.measures,
      patientCount: request.patientIds.length,
      startDate: request.startDate,
      endDate: request.endDate,
      createDate: new Date().toISOString(),
      status: 'Complete'
    };
  }
}

function ids(count: number): string[] {
  return Array.from({length: count}, (_, i) => `SIMULATED-PATIENT-${String(i + 1).padStart(4, '0')}`);
}

function tick(): Promise<void> {
  // A little latency, so loading states are actually exercised in development.
  return new Promise(resolve => setTimeout(resolve, LATENCY_MS));
}

function immediate<T>(value: T): Operation<T> {
  return {
    state: 'succeeded',
    result: () => Promise.resolve(value),
    cancel: () => undefined
  };
}

function isValidHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

// Inverse of FhirStep's buildIso8601Duration: PxDTyHzM -> [days, hours, minutes].
function parseIso8601Duration(duration?: string): [number | undefined, number | undefined, number | undefined] {
  const match = duration?.match(/^P(\d+)DT(\d+)H(\d+)M$/);
  if (!match) {
    return [undefined, undefined, undefined];
  }

  return [Number(match[1]), Number(match[2]), Number(match[3])];
}
