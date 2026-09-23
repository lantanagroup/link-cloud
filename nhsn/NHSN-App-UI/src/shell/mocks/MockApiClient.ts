import type {ApiClient, DraftEnvelope} from '../../core/api/ApiClient';
import type {FacilityDraft} from '../../core/onboarding/types';
import {createEmptyDraft, migrateDraft} from '../../core/onboarding/types';
import type {Operation} from '../../core/api/http';
import type * as C from '../../core/api/contracts';
import {ENCOUNTER_REFERENCE_CODES} from './fixtures/encounterReferenceCodes';
import {isValidHttpUrl} from '../../core/onboarding/steps/fhir/validate';

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

// Mirrors ReferenceDataService.UsTimezoneDefinitions on the BFF, which in turn matches the
// onboarding POC's own US_TIME_ZONES list verbatim: every IANA zone for the US and its
// territories, one entry per distinct zone rather than one per region (several, like the
// Indiana and North Dakota counties, have historically diverged on DST even though they
// currently agree).
const US_TIMEZONE_DEFINITIONS: ReadonlyArray<{id: string; label: string}> = [
  {id: 'America/New_York', label: 'Eastern Time'},
  {id: 'America/Detroit', label: 'Eastern Time'},
  {id: 'America/Kentucky/Louisville', label: 'Eastern Time'},
  {id: 'America/Kentucky/Monticello', label: 'Eastern Time'},
  {id: 'America/Indiana/Indianapolis', label: 'Eastern Time'},
  {id: 'America/Indiana/Vincennes', label: 'Eastern Time'},
  {id: 'America/Indiana/Winamac', label: 'Eastern Time'},
  {id: 'America/Indiana/Marengo', label: 'Eastern Time'},
  {id: 'America/Indiana/Petersburg', label: 'Eastern Time'},
  {id: 'America/Indiana/Vevay', label: 'Eastern Time'},
  {id: 'America/Indiana/Tell_City', label: 'Central Time'},
  {id: 'America/Indiana/Knox', label: 'Central Time'},
  {id: 'America/Chicago', label: 'Central Time'},
  {id: 'America/Menominee', label: 'Central Time'},
  {id: 'America/North_Dakota/Center', label: 'Central Time'},
  {id: 'America/North_Dakota/New_Salem', label: 'Central Time'},
  {id: 'America/North_Dakota/Beulah', label: 'Central Time'},
  {id: 'America/Denver', label: 'Mountain Time'},
  {id: 'America/Boise', label: 'Mountain Time'},
  {id: 'America/Phoenix', label: 'Mountain Time (no DST)'},
  {id: 'America/Los_Angeles', label: 'Pacific Time'},
  {id: 'America/Anchorage', label: 'Alaska Time'},
  {id: 'America/Juneau', label: 'Alaska Time'},
  {id: 'America/Sitka', label: 'Alaska Time'},
  {id: 'America/Metlakatla', label: 'Alaska Time'},
  {id: 'America/Yakutat', label: 'Alaska Time'},
  {id: 'America/Nome', label: 'Alaska Time'},
  {id: 'America/Adak', label: 'Hawaii-Aleutian Time'},
  {id: 'Pacific/Honolulu', label: 'Hawaii Time (no DST)'},
  {id: 'America/Puerto_Rico', label: 'Atlantic Time (Puerto Rico / US Virgin Islands)'},
  {id: 'Pacific/Guam', label: 'Chamorro Time (Guam)'},
  {id: 'Pacific/Saipan', label: 'Chamorro Time (N. Mariana Islands)'},
  {id: 'Pacific/Pago_Pago', label: 'Samoa Time (American Samoa)'}
];

function currentUtcOffsetMinutes(timeZone: string, at: Date): number {
  const offsetPart = new Intl.DateTimeFormat('en-US', {timeZone, timeZoneName: 'longOffset'})
    .formatToParts(at)
    .find(part => part.type === 'timeZoneName')?.value;
  const match = /GMT([+-])(\d{2}):(\d{2})/.exec(offsetPart ?? '');
  if (!match) {
    return 0;
  }
  const sign = match[1] === '-' ? -1 : 1;
  return sign * (Number(match[2]) * 60 + Number(match[3]));
}

function formatUtcOffset(totalMinutes: number): string {
  const sign = totalMinutes < 0 ? '-' : '+';
  const magnitude = Math.abs(totalMinutes);
  const hours = Math.floor(magnitude / 60).toString().padStart(2, '0');
  const minutes = (magnitude % 60).toString().padStart(2, '0');
  return `${sign}${hours}:${minutes}`;
}

// Sorted by current offset, ascending: most-negative (e.g. Pago Pago) first, most-positive
// (e.g. Guam) last. Ties - the many zones sharing a region's current offset - keep the
// array's declaration order, since Array#sort is stable.
function buildTimezones(): C.Timezone[] {
  const now = new Date();
  return US_TIMEZONE_DEFINITIONS.map(zone => ({...zone, offsetMinutes: currentUtcOffsetMinutes(zone.id, now)}))
    .sort((a, b) => a.offsetMinutes - b.offsetMinutes)
    .map(zone => ({id: zone.id, displayName: `${zone.id} — (UTC${formatUtcOffset(zone.offsetMinutes)}) ${zone.label}`}));
}

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
    private readonly facilityName = 'Mock Facility',
    // Default matches every non-development environment today.
    private readonly capabilities: C.Capabilities = {
      patientListWithNames: false,
      fhirConnectionProbe: false,
      sftpFileListing: false,
      onboardingRevisit: false
    }
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
        fhirConnectionProbe: false,
        sftpFileListing: false,
        onboardingRevisit: false
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

  // The one call this mock will not answer. The import package is the vendor's reviewed
  // documents, copied byte for byte out of the BFF's StaticAssets - a facility extracts the
  // real sheet and the real instructions, so there is nothing here that could stand in for
  // them. Faking it would produce a zip of invented files, which is worse than no download.
  // Switch the harness to live mode to exercise this against the BFF.
  async exportDraft(): Promise<Blob> {
    await tick();
    throw new Error('The import package is served by the BFF. Switch the harness to live mode to download it.');
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
        locationMethods: ['location-identifier', 'custom-fhir-path'],
        documentKeys: {
          censusInstructions: 'epic-census-instructions',
          jwksInstructions: 'epic-jwks-instructions',
          locationOrgResolution: 'location-org-resolution'
        },
        hslocSourceLabel: 'location.identifier.value'
      },
      {
        vendor: 'Cerner',
        displayName: 'Cerner',
        censusAcquisition: 'Sftp',
        patientListKeys: [],
        locationMethods: ['location-type', 'managing-org', 'custom-fhir-path'],
        documentKeys: {
          censusInstructions: 'cerner-census-instructions',
          jwksInstructions: 'cerner-jwks-instructions',
          locationOrgResolution: 'location-org-resolution'
        },
        hslocSourceLabel: 'location.identifier.alias'
      }
    ];
  }

  async getTimezones(): Promise<C.Timezone[]> {
    await tick();
    return buildTimezones();
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

  async getEncounterCodes(query?: string): Promise<C.EncounterCode[]> {
    await tick();
    const q = query?.trim().toLowerCase();
    if (!q) {
      return ENCOUNTER_REFERENCE_CODES;
    }
    return ENCOUNTER_REFERENCE_CODES.filter(c =>
      `${c.system} ${c.code} ${c.display} ${c.category ?? ''} ${c.categoryName ?? ''}`
        .toLowerCase()
        .includes(q)
    );
  }

  async lookupEncounterCode(system: string, code: string): Promise<C.EncounterCodeDetail | null> {
    await tick();
    const match = ENCOUNTER_REFERENCE_CODES.find(c => c.system === system && c.code === code);
    if (!match) {
      return null;
    }
    return {system, code, display: match.display, name: `${system} (simulated)`, version: '1.0.0-simulated'};
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
    const count = 5 + Math.floor(Math.random() * 46);
    return {listKey: key, patientCount: count, patients: patients(count), simulated: true};
  }

  async queryPatientLists(): Promise<C.CensusListResult[]> {
    const keys: C.CensusListKey[] = [
      'admit-lt-24',
      'admit-24-to-48',
      'admit-gt-48',
      'discharge-lt-24',
      'discharge-24-to-48',
      'discharge-gt-48'
    ];
    return Promise.all(keys.map(key => this.queryPatientList(key)));
  }

  async listSftpFiles(): Promise<C.SftpFile[]> {
    await tick();
    const queriedAt = new Date().toISOString();
    const fileCount = 2 + Math.floor(Math.random() * 4);
    let patientSeq = 0;
    return Array.from({length: fileCount}, (_, fileIndex) => {
      const patientCount = 3 + Math.floor(Math.random() * 13);
      const patientIds = Array.from({length: patientCount}, () => {
        patientSeq += 1;
        return `SIMULATED-PATIENT-${String(patientSeq).padStart(4, '0')}`;
      });
      return {
        fileName: `census_extract_${fileIndex + 1}_${queriedAt.slice(0, 10)}.csv`,
        queriedAt,
        patientIds,
        simulated: true
      };
    });
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

  async getLocationCandidates(method: C.LocationMethod): Promise<C.LocationCandidate[]> {
    await tick();
    if (method === 'location-type') {
      const typeCodings: C.LocationTypeCoding[] = [
        {system: 'https://fhir.cerner.com/ecosystem/codeSet/72', code: '783', display: 'Facility(s)'}
      ];
      // Matches the POC's fixture (MOCK_CERNER_SITE_LOCATION_NAMES) exactly - the reference this
      // screen was built against - so the full set of results shows, not a shortened stand-in.
      const names = [
        this.facilityName || 'Unnamed Facility',
        'Resurrection Medical Center',
        'UI Health',
        'Endeavor Health',
        'Gottlieb Memorial',
        'Loretto Hospital',
        'Community First Medical',
        'Humboldt Health'
      ];
      return names.map((display, index) => ({
        id: `SIMULATED-LOC-${String(index + 1).padStart(3, '0')}`,
        display,
        typeText: 'Facility(s)',
        typeCodings
      }));
    }

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

  async getMrnIntakeOptions(): Promise<C.MrnIntakeOptions> {
    await tick();
    return {
      multipleMrnTypes: [
        {value: 'empi', labelKey: 'onboarding:mrnIntake.multipleMrn.types.empi'},
        {value: 'legacy', labelKey: 'onboarding:mrnIntake.multipleMrn.types.legacy'},
        {value: 'facility', labelKey: 'onboarding:mrnIntake.multipleMrn.types.facility'},
        {value: 'visit', labelKey: 'onboarding:mrnIntake.multipleMrn.types.visit'},
        {value: 'other', labelKey: 'onboarding:mrnIntake.multipleMrn.types.other'}
      ],
      varianceTypes: [
        {value: 'facility', labelKey: 'onboarding:mrnIntake.variance.types.facility'},
        {value: 'campus', labelKey: 'onboarding:mrnIntake.variance.types.campus'},
        {value: 'setting', labelKey: 'onboarding:mrnIntake.variance.types.setting'},
        {value: 'ehr_instance', labelKey: 'onboarding:mrnIntake.variance.types.ehrInstance'},
        {value: 'other', labelKey: 'onboarding:mrnIntake.variance.types.other'}
      ],
      changeTypes: [
        {value: 'merge', labelKey: 'onboarding:mrnIntake.changes.types.merge'},
        {value: 'transfer', labelKey: 'onboarding:mrnIntake.changes.types.transfer'},
        {value: 'correction', labelKey: 'onboarding:mrnIntake.changes.types.correction'},
        {value: 'other', labelKey: 'onboarding:mrnIntake.changes.types.other'}
      ]
    };
  }

  async getPatientIdentifiers(): Promise<C.PatientIdentifier[]> {
    await tick();
    // Two identifiers per patient - an enterprise MPI id (fully populated) and a legacy MRN
    // (missing use/assigner/period, so the "N/A" rendering is exercisable) - deliberately not
    // every field on every element, matching how a real EHR's identifiers rarely agree on shape.
    return ids(3).map((patientId, index) => ({
      patientId,
      elements: [
        {
          value: `MPI-${1000 + index}`,
          type: 'MR (Medical record number)',
          system: 'http://example.invalid/fhir/identifier/empi',
          use: 'usual',
          assigner: this.facilityName,
          periodStart: '2019-01-01'
        },
        {
          value: `SIM-${patientId}`,
          type: 'MR (Medical record number)',
          system: 'http://example.invalid/fhir/identifier/legacy-mrn'
        }
      ]
    }));
  }

  // ------------------------------------------------------------ reporting

  // Pending, matching the real request: the id is assigned synchronously and
  // the report is generated behind it. A step that navigated on Complete here
  // would be built against a fiction.
  async requestReport(request: C.ReportRequest): Promise<Operation<C.ReportSummary>> {
    await tick();
    return immediate({...this.buildReport(request), status: 'Pending'});
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

  async getPatientMappingEvidence(): Promise<C.PatientMappingEvidence> {
    await tick();
    return {
      locationOrg: {
        encounterCount: 3,
        orgEncounterCount: 3,
        assumedOrgEncounterCount: 0,
        matches: [
          {locationId: '783', locationName: 'UI Health', locationAlias: 'UI Health', isOrgLocation: true},
          {locationId: '783', locationName: 'Endeavor Health', locationAlias: 'Endeavor Health', isOrgLocation: true}
        ]
      },
      codeMaps: [
        {sourceSystem: 'http://mysite.org/fhir/encounter-type', targetSystem: 'CPT', mappedCount: 2, unmappedCount: 1, failureCount: 0, unmappedCodes: ['UNMAPPED-32']},
        // targetSystem 'HSLOC' is what the Report Details "HSLOC Mapping" modal filters on --
        // see isHslocCodeMap in ReportResultsStep.tsx -- so mock mode exercises its "+ Add Mapping"
        // flow for an unmapped value the same way a real unmapped HSLOC location code would.
        {sourceSystem: 'Location.identifier', targetSystem: 'HSLOC', mappedCount: 0, unmappedCount: 1, failureCount: 0, unmappedCodes: ['UNMAPPED-LOC-0']}
      ]
    };
  }

  async getPatientPreQualResults(): Promise<C.PreQualIssue[]> {
    await tick();
    return [];
  }

  async getReportPatients(): Promise<C.ReportPatientEntry[]> {
    await tick();
    return ids(3).map(patientId => ({
      patientId,
      reportingStatus: 'PassedValidation' as const,
      resourceCount: 12,
      resourceCountsByType: {Patient: 1, Encounter: 4, Location: 2, MedicationRequest: 5},
      locationOrgMapped: true,
      encounterMapped: true,
      hslocMapped: true,
      hasPreQualResults: true,
      measureReports: [
        {
          reportType: 'NHSNGlycemicControlHypoglycemicInitialPopulation',
          resourceCount: 12,
          resourceCountsByType: {Patient: 1, Encounter: 4, Location: 2, MedicationRequest: 5}
        }
      ]
    }));
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
    // Shaped like DataAcquisition's real query plan JSON (PlanName/EHRDescription/LookBack plus
    // numerically-keyed Initial/SupplementalQueries dictionaries) so the structured modal view --
    // not just its raw-JSON fallback -- is exercised in mock mode.
    const planJson = JSON.stringify({
      PlanName: 'Simulated-ACHMonthly',
      EHRDescription: 'Epic (simulated)',
      LookBack: 'P0D',
      InitialQueries: {
        '0': {
          ResourceType: 'Patient',
          QueryConfigType: 'Parameter',
          Parameters: [{Name: '_id', Variable: 'patientId'}]
        },
        '1': {
          ResourceType: 'Encounter',
          QueryConfigType: 'Parameter',
          Parameters: [
            {Name: 'patient', Variable: 'patientId'},
            {Name: 'date', Literal: 'ge2026-01-01'}
          ]
        }
      },
      SupplementalQueries: {
        '0': {
          ResourceType: 'Location',
          QueryConfigType: 'Reference',
          OperationType: 'Search',
          Paged: true,
          Parameters: [{Name: '_id', Variable: 'locationId'}]
        }
      }
    });
    return {reportId, planJson};
  }

  async getAcquisitionLogs(): Promise<C.AcquisitionLogEntry[]> {
    await tick();
    return ids(3).flatMap(patientId => [
      {patientId, resource: 'Patient', queryPhase: 'Initial', queryType: 'Read', parameters: [], status: 'Completed'},
      {
        patientId,
        resource: 'Encounter',
        queryPhase: 'Initial',
        queryType: 'Search',
        parameters: [`patient=${patientId}`, 'date=ge2026-01-01'],
        status: 'Completed'
      },
      {
        patientId,
        resource: 'Location',
        queryPhase: 'Supplemental',
        queryType: 'Search',
        parameters: ['_id=SIMULATED-LOC-1'],
        status: 'Pending'
      }
    ]);
  }

  async exportReportSummary(): Promise<Blob> {
    await tick();
    return new Blob(['simulated report summary'], {type: 'text/plain'});
  }

  async exportPatientReport(reportId: string, patientId: string, reportType: string): Promise<Blob> {
    await tick();
    const measureReport = {
      resourceType: 'MeasureReport',
      id: `${reportId}-${patientId}-${reportType}`,
      status: 'complete',
      type: 'individual',
      measure: reportType,
      subject: {reference: `Patient/${patientId}`},
      extension: [{url: 'urn:nhsn-link:reportingStatus', valueString: 'PassedValidation (simulated)'}]
    };
    const patient = {resourceType: 'Patient', id: patientId};
    const ndjson = [measureReport, patient].map(resource => JSON.stringify(resource)).join('\n');
    return new Blob([ndjson], {type: 'application/x-ndjson'});
  }

  async regenerateReport(reportId: string): Promise<Operation<C.ReportSummary>> {
    const summary = this.buildReport({measures: [], startDate: '', endDate: '', patientIds: []});
    return immediate({...summary, regeneratedFrom: reportId});
  }

  async getReportAcknowledgement(): Promise<boolean | null> {
    await tick();
    return null;
  }

  async acknowledgeReport(): Promise<void> {
    await tick();
    // Intentionally stores nothing -- mirrors acknowledgeCensus above.
  }

  // ------------------------------------------------------------ reporting plan

  async getReportingPlan(): Promise<C.ReportingPlan> {
    await tick();
    return {
      rows: [{month: 'January', year: 2026, measures: ['Sample Measure One (simulated)']}]
    };
  }

  async getAvailableMeasures(): Promise<C.AvailableMeasure[]> {
    await tick();
    return [
      {
        name: 'Glycemic Control (simulated)',
        digitalQualityMeasure: 'NHSNGlycemicControlHypoglycemicInitialPopulation'
      },
      {
        name: 'Acute Care Hospital Monthly (simulated)',
        digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
      }
    ];
  }

  async getJwksInstructionsPdf(vendor: string): Promise<Blob> {
    await tick();
    const body = `Simulated ${vendor} JWKS instructions PDF.\n\nNo backend is connected in mock mode — against the real BFF this downloads the actual instructions PDF.`;
    return new Blob([body], {type: 'text/plain;charset=utf-8'});
  }

  async getLocationOrgResolutionPdf(): Promise<Blob> {
    await tick();
    const body = 'Simulated Location Org Resolution PDF.\n\nNo backend is connected in mock mode — against the real BFF this downloads the actual instructions PDF.';
    return new Blob([body], {type: 'text/plain;charset=utf-8'});
  }

  async getCensusInstructionsPdf(vendor: string): Promise<Blob> {
    await tick();
    const body = `Simulated ${vendor} census instructions PDF.\n\nNo backend is connected in mock mode — against the real BFF this downloads the actual instructions PDF.`;
    return new Blob([body], {type: 'text/plain;charset=utf-8'});
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

function patients(count: number): C.CensusPatient[] {
  return ids(count).map((id, i) => ({id, name: `Simulated Patient ${i + 1}`}));
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
