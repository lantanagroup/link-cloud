import type {
  CodeMapEvidence,
  EncounterMapping,
  HslocMapping,
  LocationMethod,
  PatientMappingEvidence,
  ReportingStatus,
  ReportPatientEntry,
} from '../../../api/contracts';
import type { LocationOrgDraft } from '../../types';
import { hashToColor } from './pieChart';
import { toStatusCategory, type ReportStatusSlice } from './reportStatus';

/**
 * `PatientMappingEvidence.codeMaps` backs both the HSLOC and Encounter mapping indicators (see its
 * doc comment in contracts.ts) with no field of its own naming which one a given entry is for.
 * HSLOC's target vocabulary is the fixed NHSN HSLOC code list -- Normalization names it literally
 * "HSLOC" as the target system -- while Encounter mapping always targets a real coding system
 * (CPT, SNOMED CT, ...). Splitting on that literal is the only signal available without a new field.
 */
export function isHslocCodeMap(codeMap: CodeMapEvidence): boolean {
  return codeMap.targetSystem.trim().toUpperCase() === 'HSLOC';
}

/**
 * Whether a patient's encounter mapping evidence is fully covered by the facility's live
 * mappings, even if the evidence itself (a snapshot taken when the report ran) still lists
 * those codes as unmapped -- lets a mapping added after report generation flip the "Found"
 * pill without waiting on the backend to re-evaluate the report.
 */
export function isEncounterMappingResolved(
  evidence: PatientMappingEvidence,
  mappings: EncounterMapping[],
): boolean {
  const mappedKeys = new Set(
    mappings.map((mapping) => `${mapping.system}|${mapping.code}`),
  );
  return evidence.codeMaps
    .filter((codeMap) => !isHslocCodeMap(codeMap))
    .every((codeMap) =>
      codeMap.unmappedCodes.every((code) =>
        mappedKeys.has(`${codeMap.sourceSystem}|${code}`),
      ),
    );
}

/** Same idea as isEncounterMappingResolved, for HSLOC -- HslocMapping has no system field, so unmapped codes are cross-referenced by sourceCode alone. */
export function isHslocMappingResolved(
  evidence: PatientMappingEvidence,
  mappings: HslocMapping[],
): boolean {
  const mappedCodes = new Set(mappings.map((mapping) => mapping.sourceCode));
  return evidence.codeMaps
    .filter(isHslocCodeMap)
    .every((codeMap) =>
      codeMap.unmappedCodes.every((code) => mappedCodes.has(code)),
    );
}

/** A field's `t` narrowed to the plain-string-key shape the sheet builders below need. */
export type TFn = (key: string) => string;

export interface AcquisitionLogFilters {
  patientId: string;
  resource: string;
  queryPhase: string;
  queryType: string;
  status: string;
}

export const EMPTY_ACQUISITION_LOG_FILTERS: AcquisitionLogFilters = {
  patientId: '',
  resource: '',
  queryPhase: '',
  queryType: '',
  status: '',
};

/**
 * Patients scoped to one dQM tab. Link has no per-dQM validation outcome -- ReportingStatus is one
 * value for the whole patient -- but each patient's measureReports do carry a real per-dQM
 * resource count, so population and resource counts can honestly narrow to the active tab even
 * though the status pie underneath is still the patient's one overall status.
 */
export function patientsForDqm(
  patients: ReportPatientEntry[],
  dqmId: string | undefined,
): ReportPatientEntry[] {
  if (!dqmId) {
    return patients;
  }
  return patients.filter((patient) =>
    patient.measureReports.some((report) => report.reportType === dqmId),
  );
}

function measureReportFor(
  patient: ReportPatientEntry,
  dqmId: string | undefined,
) {
  return dqmId
    ? patient.measureReports.find((report) => report.reportType === dqmId)
    : undefined;
}

export interface PatientStatusRow {
  patientId: string;
  fhirResourceCount: number;
  reportStatusKey: string;
  /** The raw status, kept alongside reportStatusKey for the Patient Status Timeline modal. */
  reportingStatus: ReportingStatus;
  hasPreQualResults: boolean;
  locationOrgFound: boolean;
  hslocFound: boolean;
  encounterFound: boolean;
  resourceCountsByType: Record<string, number>;
}

// Resource counts scope to the active DQM tab when a matching per-measure report exists --
// falling back to the patient's overall totals covers the "no dQM selected yet" render and any
// patient whose measureReports don't (yet) carry that dQM.
export function toPatientRows(
  patients: ReportPatientEntry[],
  dqmId: string | undefined,
): PatientStatusRow[] {
  return patients.map((patient) => {
    const measureReport = measureReportFor(patient, dqmId);
    return {
      patientId: patient.patientId,
      fhirResourceCount: measureReport?.resourceCount ?? patient.resourceCount,
      reportStatusKey: toStatusCategory(patient.reportingStatus),
      reportingStatus: patient.reportingStatus,
      hasPreQualResults: patient.hasPreQualResults,
      locationOrgFound: patient.locationOrgMapped,
      hslocFound: patient.hslocMapped,
      encounterFound: patient.encounterMapped,
      resourceCountsByType:
        measureReport?.resourceCountsByType ?? patient.resourceCountsByType,
    };
  });
}

/** The facility's configured Location Org mapping table, shaped per-method like LocationOrgStep's own lists. */
export interface LocationOrgConfigInfo {
  method: LocationMethod;
  headers: string[];
  rows: string[][];
}

// Mirrors the onboarding POC's locationOrgConfigInfo(): which configured list backs the "Configured
// Location Org Mappings" table in the Report Details modal depends on the facility's chosen method.
// Custom FHIRPath (and no method at all) has no structured list to show, matching the POC.
export function locationOrgConfigInfo(
  locationOrg: LocationOrgDraft,
  t: TFn,
): LocationOrgConfigInfo | null {
  if (locationOrg.method === 'location-identifier') {
    return {
      method: locationOrg.method,
      headers: [
        t('onboarding:locationOrg.locationIdentifier.systemLabel'),
        t('onboarding:locationOrg.locationIdentifier.codeLabel'),
      ],
      rows: (locationOrg.locationIdentifiers ?? []).map((row) => [
        row.system,
        row.code,
      ]),
    };
  }
  if (locationOrg.method === 'location-type') {
    return {
      method: locationOrg.method,
      headers: [
        t('onboarding:locationOrg.locationType.codeLabel'),
        t('onboarding:locationOrg.locationType.aliasLabel'),
      ],
      rows: (locationOrg.locationTypes ?? []).map((row) => [
        row.code,
        row.alias,
      ]),
    };
  }
  if (locationOrg.method === 'managing-org') {
    return {
      method: locationOrg.method,
      headers: [t('onboarding:locationOrg.managingOrg.listLabel')],
      rows: (locationOrg.managingOrganizationIds ?? []).map((value) => [value]),
    };
  }
  return null;
}

// Resource type names are FHIR resource types (Patient, Encounter, MedicationRequest, ...) as Report
// returns them -- data, not UI copy, so they render as-is rather than through an i18n lookup.
const RESOURCE_TYPE_COLOR_PALETTE = [
  '#0b5cab',
  '#7c3aed',
  '#15803d',
  '#b45309',
  '#be185d',
  '#0f766e',
  '#4338ca',
  '#a16207',
];

function resourceTypeColor(resourceType: string): string {
  return hashToColor(resourceType, RESOURCE_TYPE_COLOR_PALETTE);
}

export function buildResourceBreakdown(
  counts: Record<string, number>,
): ReportStatusSlice[] {
  const total = Object.values(counts).reduce((sum, value) => sum + value, 0);
  if (total <= 0) {
    return [];
  }

  return Object.entries(counts)
    .filter(([, count]) => count > 0)
    .map(([resourceType, count]) => ({
      labelKey: resourceType,
      color: resourceTypeColor(resourceType),
      count,
      percent: Math.round((count / total) * 100),
    }));
}
