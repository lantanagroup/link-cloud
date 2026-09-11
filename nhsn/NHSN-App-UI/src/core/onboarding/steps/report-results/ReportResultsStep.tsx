import React, {useCallback, useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {
  CodeMapEvidence,
  HslocCode,
  HslocMapping,
  LocationMethod,
  ReportDetail,
  ReportingStatus,
  ReportPatientEntry,
  ReportStatus,
  ReportSummary,
  PatientMappingEvidence,
  QueryPlan,
  AcquisitionLogEntry
} from '../../../api/contracts';
import {HttpError} from '../../../api/http';
import {PatientStatusTimelineModal} from './PatientStatusTimeline';
import {PreQualResultsModal} from './PreQualResults';
import {
  Button,
  CheckboxField,
  MessageContainer,
  Modal,
  NHSNLoadingIndicator,
  PageHeader,
  Select,
  StepActions,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import type {LocationOrgDraft} from '../../types';
import {buildGroups} from '../encounter/EncounterStep';
import {METHOD_LABEL_KEYS} from '../location-org/LocationOrgStep';
import {PLACEHOLDER_MEASURES} from '../report/placeholderMeasures';
import {parseQueryPlan, type ParsedQueryPlan, type ParsedQueryPlanQuery} from './queryPlan';
import {buildXlsxBlob, downloadBlob, type XlsxSheet} from './reportExport';

/**
 * `PatientMappingEvidence.codeMaps` backs both the HSLOC and Encounter mapping indicators (see its
 * doc comment in contracts.ts) with no field of its own naming which one a given entry is for.
 * HSLOC's target vocabulary is the fixed NHSN HSLOC code list -- Normalization names it literally
 * "HSLOC" as the target system -- while Encounter mapping always targets a real coding system
 * (CPT, SNOMED CT, ...). Splitting on that literal is the only signal available without a new field.
 */
function isHslocCodeMap(codeMap: CodeMapEvidence): boolean {
  return codeMap.targetSystem.trim().toUpperCase() === 'HSLOC';
}

/** A field's `t` narrowed to the plain-string-key shape the sheet builders below need. */
type TFn = (key: string) => string;

interface AcquisitionLogFilters {
  patientId: string;
  resource: string;
  queryPhase: string;
  queryType: string;
  status: string;
}

const EMPTY_ACQUISITION_LOG_FILTERS: AcquisitionLogFilters = {
  patientId: '',
  resource: '',
  queryPhase: '',
  queryType: '',
  status: ''
};

// nhsn-react-core's Badge defaults to a small `shape="circle"` icon-count indicator and is
// `aria-hidden` - it's the wrong shape for a readable status label, so status renders as its own
// soft pill instead (pale background, bold saturated text), matching the onboarding POC.
const STATUS_PILL_CLASS: Record<ReportStatus, string> = {
  Complete: 'nhsn-link__status-pill--complete',
  Pending: 'nhsn-link__status-pill--pending',
  Failed: 'nhsn-link__status-pill--failed',
  Cancelled: 'nhsn-link__status-pill--cancelled'
};

// Reuses the same pill classes for the per-patient report status column. Link's ReportingStatus has
// no "Critical Failure" value -- PatientIdentified/PendingValidation (validation hasn't run yet)
// share the pending pill instead of inventing a category the platform never produces.
const STATUS_PILL_CLASS_BY_KEY: Record<string, string> = {
  notEligible: 'nhsn-link__status-pill--pending',
  pendingValidation: 'nhsn-link__status-pill--pending',
  failedValidation: 'nhsn-link__status-pill--failed',
  passedValidation: 'nhsn-link__status-pill--complete'
};

/**
 * The NHSN measure names Report Results/Details show for a report. Prefers the exact selection
 * made when the report was requested (kept in the draft as real measure names, since Report only
 * knows the resolved dQM); falls back to reversing DIGITAL_QUALITY_MEASURE_BY_MEASURE /
 * DQM_INFO_BY_REAL_ID below from the report's real dQM ids when that record isn't available --
 * e.g. a report generated in another session -- which only resolves the fixed demo measure names
 * those tables know about, but is still more useful than the raw dQM id.
 */
function friendlyMeasuresFor(
  dqmMeasures: string[],
  reportId: string,
  requestedMeasuresByReportId?: Record<string, string[]>
): string[] {
  const requested = requestedMeasuresByReportId?.[reportId];
  if (requested && requested.length > 0) {
    return requested;
  }

  const dqmTabNames = new Set(
    dqmMeasures.map(dqmId => DQM_INFO_BY_REAL_ID[dqmId]?.name).filter((name): name is string => Boolean(name))
  );
  const byDqmTab = Object.entries(DIGITAL_QUALITY_MEASURE_BY_MEASURE)
    .filter(([, tabName]) => dqmTabNames.has(tabName))
    .map(([measureName]) => measureName);
  return byDqmTab.length > 0 ? byDqmTab : dqmMeasures;
}

// Report generation has no completion signal wired up downstream in this environment -- every ad
// hoc report stays Pending forever, so Complete is otherwise never seen. Stable per report id
// (not re-rolled every render) rather than truly random, so a report doesn't flicker between
// looking Pending and Complete on refresh. Delete this once Report reports real completion.
// A report shown as Complete (see demoDisplayStatus below) can't honestly still have patients
// sitting in PatientIdentified/PendingValidation -- those mean "hasn't reached a verdict yet",
// which contradicts the report itself being done. Resolves those two to a terminal outcome,
// stable per patient id so it doesn't flicker on refresh. Passed/Failed/NotReportable are already
// terminal and pass through untouched.
function demoDisplayReportingStatus(status: ReportingStatus, patientId: string, reportIsComplete: boolean): ReportingStatus {
  if (!reportIsComplete || status === 'PassedValidation' || status === 'FailedValidation' || status === 'NotReportable') {
    return status;
  }
  const terminalOutcomes: ReportingStatus[] = ['PassedValidation', 'FailedValidation', 'NotReportable'];
  let hash = 0;
  for (let i = 0; i < patientId.length; i++) {
    hash = (hash * 31 + patientId.charCodeAt(i)) >>> 0;
  }
  return terminalOutcomes[hash % terminalOutcomes.length];
}

function demoDisplayStatus(status: ReportStatus, reportId: string): ReportStatus {
  if (status !== 'Pending') {
    return status;
  }
  let hash = 0;
  for (let i = 0; i < reportId.length; i++) {
    hash = (hash * 31 + reportId.charCodeAt(i)) >>> 0;
  }
  return hash % 2 === 0 ? 'Complete' : 'Pending';
}

// Matches the onboarding POC's fixed measure -> digital quality measure assignment and the external
// pages each one links to.
const DIGITAL_QUALITY_MEASURE_BY_MEASURE: Record<string, string> = {
  'Glycemic Control': 'ACH Monthly',
  'Adult Sepsis Bacteria & Fungemia': 'ACH Monthly',
  'C. Difficile Infection': 'ACH Monthly',
  'Respiratory Pathogens Surveillance (RPS)': 'ACH Daily',
  'Antimicrobial Use and Resistance (AU/AR)': 'LTC Monthly'
};

const DQM_INFO_BY_REAL_ID: Record<string, {name: string; url: string}> = {
  NHSNGlycemicControlHypoglycemicInitialPopulation: {
    name: 'ACH Monthly',
    url: 'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalMonthlyInitialPopulation.html'
  },
  NHSNAcuteCareHospitalMonthlyInitialPopulation: {
    name: 'ACH Monthly',
    url: 'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalMonthlyInitialPopulation.html'
  },
  NHSNAcuteCareHospitalDailyInitialPopulation: {
    name: 'ACH Daily',
    url: 'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalDailyInitialPopulation.html'
  },
  NHSNLongTermCareMonthlyInitialPopulation: {
    name: 'LTC Monthly',
    url: 'https://measures-ci.nhsnlink.org/Measure-NHSNLongTermCareMonthlyInitialPopulation.html'
  }
};

// A DQM tab ("ACH Monthly") to the real report type Report stores measure reports under
// ("NHSNAcuteCareHospitalMonthlyInitialPopulation"). Fixed here directly, matching
// DQM_INFO_BY_REAL_ID above -- previously derived from the placeholder measure table, which is
// gone now that the picker reads the facility's real reporting plan instead of a fixed 5-measure
// list.
const REAL_REPORT_TYPE_BY_DQM_NAME: Record<string, string> = {
  'ACH Monthly': 'NHSNAcuteCareHospitalMonthlyInitialPopulation',
  'ACH Daily': 'NHSNAcuteCareHospitalDailyInitialPopulation',
  'LTC Monthly': 'NHSNLongTermCareMonthlyInitialPopulation'
};

// The reverse of the map above, for turning a patient's real per-dQM report type back into the
// DQM tab name it belongs under -- used by the Export Report Summary sheet, which isn't scoped to
// whichever tab happens to be active on screen.
const DQM_NAME_BY_REPORT_TYPE: Record<string, string> = Object.fromEntries(
  Object.entries(REAL_REPORT_TYPE_BY_DQM_NAME).map(([dqmName, reportType]) => [reportType, dqmName])
);

/**
 * Patients scoped to one dQM tab. Link has no per-dQM validation outcome -- ReportingStatus is one
 * value for the whole patient -- but each patient's measureReports do carry a real per-dQM
 * resource count, so population and resource counts can honestly narrow to the active tab even
 * though the status pie underneath is still the patient's one overall status.
 */
function patientsForDqm(patients: ReportPatientEntry[], dqmName: string | undefined): ReportPatientEntry[] {
  const reportType = dqmName ? REAL_REPORT_TYPE_BY_DQM_NAME[dqmName] : undefined;
  if (!reportType) {
    return patients;
  }
  return patients.filter(patient => patient.measureReports.some(report => report.reportType === reportType));
}

function measureReportFor(patient: ReportPatientEntry, dqmName: string | undefined) {
  const reportType = dqmName ? REAL_REPORT_TYPE_BY_DQM_NAME[dqmName] : undefined;
  return reportType ? patient.measureReports.find(report => report.reportType === reportType) : undefined;
}

// Matches the onboarding POC's measureColor(): a string hash into a fixed palette, so a given
// measure name always renders the same color without a hand-maintained name -> color map.
const MEASURE_COLOR_PALETTE = ['#0b5cab', '#7c3aed', '#15803d', '#b45309', '#be185d', '#0f766e', '#4338ca', '#a16207'];

function measureColor(measure: string): string {
  let hash = 0;
  for (let i = 0; i < measure.length; i++) {
    hash = (hash * 31 + measure.charCodeAt(i)) >>> 0;
  }
  return MEASURE_COLOR_PALETTE[hash % MEASURE_COLOR_PALETTE.length];
}

interface ReportStatusSlice {
  labelKey: string;
  color: string;
  count: number;
  percent: number;
}

const STATUS_CATEGORY_COLOR: Record<string, string> = {
  notEligible: '#b45309',
  failedValidation: '#dc2626',
  pendingValidation: '#0b5cab',
  passedValidation: '#15803d'
};

// Link's ReportingStatus enum, folded into the four categories the report status pie shows.
// PatientIdentified/PendingValidation both mean "validation hasn't produced a verdict yet".
function toStatusCategory(status: ReportPatientEntry['reportingStatus']): string {
  switch (status) {
    case 'NotReportable':
      return 'notEligible';
    case 'FailedValidation':
      return 'failedValidation';
    case 'PassedValidation':
      return 'passedValidation';
    case 'PatientIdentified':
    case 'PendingValidation':
    default:
      return 'pendingValidation';
  }
}

function buildReportStatusBreakdown(patients: ReportPatientEntry[]): ReportStatusSlice[] {
  if (patients.length === 0) {
    return [];
  }

  const counts = new Map<string, number>();
  patients.forEach(patient => {
    const key = toStatusCategory(patient.reportingStatus);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  });

  return Array.from(counts.entries()).map(([labelKey, count]) => ({
    labelKey,
    color: STATUS_CATEGORY_COLOR[labelKey],
    count,
    percent: Math.round((count / patients.length) * 100)
  }));
}

function polarToCartesian(cx: number, cy: number, r: number, angleDeg: number) {
  const angleRad = ((angleDeg - 90) * Math.PI) / 180;
  return {x: cx + r * Math.cos(angleRad), y: cy + r * Math.sin(angleRad)};
}

function describePieSlice(cx: number, cy: number, r: number, startAngle: number, endAngle: number): string {
  if (endAngle - startAngle >= 359.99) {
    // A single 100% slice has no distinct start/end point for an arc - draw it as two half-circles.
    const mid = startAngle + 180;
    return [describePieSlice(cx, cy, r, startAngle, mid), describePieSlice(cx, cy, r, mid, endAngle)].join(' ');
  }
  const start = polarToCartesian(cx, cy, r, endAngle);
  const end = polarToCartesian(cx, cy, r, startAngle);
  const largeArcFlag = endAngle - startAngle <= 180 ? '0' : '1';
  return ['M', cx, cy, 'L', start.x, start.y, 'A', r, r, 0, largeArcFlag, 0, end.x, end.y, 'Z'].join(' ');
}

interface PatientStatusRow {
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
function toPatientRows(patients: ReportPatientEntry[], dqmName: string | undefined): PatientStatusRow[] {
  return patients.map(patient => {
    const measureReport = measureReportFor(patient, dqmName);
    return {
      patientId: patient.patientId,
      fhirResourceCount: measureReport?.resourceCount ?? patient.resourceCount,
      reportStatusKey: toStatusCategory(patient.reportingStatus),
      reportingStatus: patient.reportingStatus,
      hasPreQualResults: patient.hasPreQualResults,
      locationOrgFound: patient.locationOrgMapped,
      hslocFound: patient.hslocMapped,
      encounterFound: patient.encounterMapped,
      resourceCountsByType: measureReport?.resourceCountsByType ?? patient.resourceCountsByType
    };
  });
}

/** The facility's configured Location Org mapping table, shaped per-method like LocationOrgStep's own lists. */
interface LocationOrgConfigInfo {
  method: LocationMethod;
  headers: string[];
  rows: string[][];
}

// Mirrors the onboarding POC's locationOrgConfigInfo(): which configured list backs the "Configured
// Location Org Mappings" table in the Report Details modal depends on the facility's chosen method.
// Custom FHIRPath (and no method at all) has no structured list to show, matching the POC.
function locationOrgConfigInfo(locationOrg: LocationOrgDraft, t: TFn): LocationOrgConfigInfo | null {
  if (locationOrg.method === 'location-identifier') {
    return {
      method: locationOrg.method,
      headers: [t('onboarding:locationOrg.locationIdentifier.systemLabel'), t('onboarding:locationOrg.locationIdentifier.codeLabel')],
      rows: (locationOrg.locationIdentifiers ?? []).map(row => [row.system, row.code])
    };
  }
  if (locationOrg.method === 'location-type') {
    return {
      method: locationOrg.method,
      headers: [t('onboarding:locationOrg.locationType.codeLabel'), t('onboarding:locationOrg.locationType.aliasLabel')],
      rows: (locationOrg.locationTypes ?? []).map(row => [row.code, row.alias])
    };
  }
  if (locationOrg.method === 'managing-org') {
    return {
      method: locationOrg.method,
      headers: [t('onboarding:locationOrg.managingOrg.listLabel')],
      rows: (locationOrg.managingOrganizationIds ?? []).map(value => [value])
    };
  }
  return null;
}

// Resource type names are FHIR resource types (Patient, Encounter, MedicationRequest, ...) as Report
// returns them -- data, not UI copy, so they render as-is rather than through an i18n lookup.
const RESOURCE_TYPE_COLOR_PALETTE = ['#0b5cab', '#7c3aed', '#15803d', '#b45309', '#be185d', '#0f766e', '#4338ca', '#a16207'];

function resourceTypeColor(resourceType: string): string {
  let hash = 0;
  for (let i = 0; i < resourceType.length; i++) {
    hash = (hash * 31 + resourceType.charCodeAt(i)) >>> 0;
  }
  return RESOURCE_TYPE_COLOR_PALETTE[hash % RESOURCE_TYPE_COLOR_PALETTE.length];
}

function buildResourceBreakdown(counts: Record<string, number>): ReportStatusSlice[] {
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
      percent: Math.round((count / total) * 100)
    }));
}

// toLocaleDateString()/toLocaleString() format by the runtime's own locale, so the same report
// renders a different date shape depending on which machine or browser opened the page. These
// build the string by hand instead, so the format is fixed everywhere and Create Date reads as
// the same YYYY-MM-DD style as Start/End Date, just with a time appended.
function formatDate(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return iso;
  }
  const year = parsed.getFullYear();
  const month = String(parsed.getMonth() + 1).padStart(2, '0');
  const day = String(parsed.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatDateTime(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) {
    return iso;
  }
  const hours = String(parsed.getHours()).padStart(2, '0');
  const minutes = String(parsed.getMinutes()).padStart(2, '0');
  const seconds = String(parsed.getSeconds()).padStart(2, '0');
  return `${formatDate(iso)}, ${hours}:${minutes}:${seconds}`;
}

function RefreshIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 12a9 9 0 1 1-2.64-6.36" />
      <path d="M21 3v6h-6" />
    </svg>
  );
}

function ChartIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="3" y="10" width="4" height="10" />
      <rect x="10" y="6" width="4" height="14" />
      <rect x="17" y="3" width="4" height="17" />
    </svg>
  );
}

function DownloadIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M12 3v12" />
      <path d="M7 10l5 5 5-5" />
      <path d="M5 21h14" />
    </svg>
  );
}

// ---------------------------------------------------------------- report summary export
//
// Builds the "Export Report Summary" workbook: one .xlsx with a sheet per section of the
// onboarding POC's zip-of-files export (Report Summary, Selected Measures, Patient Reporting
// Status, Acquisition Log, Query Plan), rather than a zip of separate files -- see reportExport.ts.
// Each sheet covers every measure/patient/query, not just whichever DQM tab is active on screen.

function buildReportSummarySheet(detail: ReportDetail): XlsxSheet {
  const rows: Array<[string, string]> = [['Report Id', detail.reportId]];
  if (detail.regeneratedFrom) {
    rows.push(['Regenerated From', detail.regeneratedFrom]);
  }
  rows.push(
    ['Reporting Period', `${formatDate(detail.startDate)} to ${formatDate(detail.endDate)}`],
    ['Create Date', formatDateTime(detail.createDate)],
    ['Patient Count', String(detail.patientCount)],
    ['Status', demoDisplayStatus(detail.status, detail.reportId)]
  );
  return {name: 'Report Summary', headers: ['Field', 'Value'], rows};
}

function buildSelectedMeasuresSheet(detail: ReportDetail, requestedMeasuresByReportId: Record<string, string[]> | undefined): XlsxSheet {
  const measures = friendlyMeasuresFor(detail.measures, detail.reportId, requestedMeasuresByReportId);
  const rows = measures.map(measure => [measure, DIGITAL_QUALITY_MEASURE_BY_MEASURE[measure] ?? '']);
  return {name: 'Selected Measures', headers: ['NHSN Measure', 'Digital Quality Measure'], rows};
}

function buildPatientReportingStatusSheet(patients: ReportPatientEntry[], t: TFn): XlsxSheet {
  const rows = patients.flatMap(patient => {
    const reports = patient.measureReports.length > 0 ? patient.measureReports : [null];
    return reports.map(measureReport => [
      patient.patientId,
      measureReport ? (DQM_NAME_BY_REPORT_TYPE[measureReport.reportType] ?? measureReport.reportType) : '—',
      String(measureReport?.resourceCount ?? patient.resourceCount),
      t(`onboarding:reportResults.detail.statusCategories.${toStatusCategory(patient.reportingStatus)}`),
      patient.locationOrgMapped ? t('onboarding:reportResults.detail.mapping.found') : t('onboarding:reportResults.detail.mapping.notFound'),
      patient.hslocMapped ? t('onboarding:reportResults.detail.mapping.found') : t('onboarding:reportResults.detail.mapping.notFound'),
      patient.encounterMapped ? t('onboarding:reportResults.detail.mapping.found') : t('onboarding:reportResults.detail.mapping.notFound')
    ]);
  });
  return {
    name: 'Patient Reporting Status',
    headers: ['Patient Id', 'Measure', 'FHIR Resource Count', 'Report Status', 'Location Org Found', 'HSLOC Mapping Found', 'Encounter Mapping Found'],
    rows
  };
}

function buildAcquisitionLogSheet(entries: AcquisitionLogEntry[]): XlsxSheet {
  return {
    name: 'Acquisition Log',
    headers: ['Patient Id', 'Resource', 'Query Phase', 'Query Type', 'Parameters', 'Status'],
    rows: entries.map(entry => [entry.patientId, entry.resource, entry.queryPhase, entry.queryType ?? '', entry.parameters.join('; '), entry.status])
  };
}

function queryPlanRows(plan: ParsedQueryPlan): Array<Array<string>> {
  const row = (section: string, query: ParsedQueryPlanQuery) => [
    section,
    query.resourceType,
    query.queryConfigType ?? '',
    query.operationType ?? '',
    query.paged === undefined ? '' : query.paged ? 'Yes' : 'No',
    query.parameters.join('; ')
  ];
  return [...plan.initialQueries.map(query => row('Initial', query)), ...plan.supplementalQueries.map(query => row('Supplemental', query))];
}

function buildQueryPlanSheet(plan: ParsedQueryPlan): XlsxSheet {
  return {
    name: 'Query Plan',
    headers: ['Section', 'Resource Type', 'Query Type', 'Operation Type', 'Paged', 'Parameters'],
    rows: queryPlanRows(plan)
  };
}

/**
 * The Report Details modal covers the report's own fields, the selected measures' digital
 * quality measure links, and the View Query Plan / View Acquisition Log / Export Report Summary
 * actions from the onboarding POC. The patient pipeline is not built here - LEGLINK story:
 * Report Details sub-view.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */
export function ReportResultsStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifySuccess, notifyError} = useNotifications();
  const {draft, patch, saving, goTo, openView, closeView, vendorProfile} = useOnboarding();
  const reportResults = draft.reportResults;

  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  const viewingDetail = draft.currentView?.view === 'detail';
  const viewingReportId = draft.currentView?.params?.reportId;

  const [detail, setDetail] = useState<ReportDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [patients, setPatients] = useState<ReportPatientEntry[]>([]);
  const [activeDqm, setActiveDqm] = useState<string | undefined>();
  const [selectedPatientRow, setSelectedPatientRow] = useState<PatientStatusRow | null>(null);
  const [timelinePatientRow, setTimelinePatientRow] = useState<PatientStatusRow | null>(null);
  const [preQualPatientRow, setPreQualPatientRow] = useState<PatientStatusRow | null>(null);
  const [mappingEvidenceColumn, setMappingEvidenceColumn] = useState<'locationOrg' | 'hsloc' | 'encounter' | null>(null);
  const [mappingEvidencePatientId, setMappingEvidencePatientId] = useState<string | null>(null);
  const [mappingEvidence, setMappingEvidence] = useState<PatientMappingEvidence | null>(null);
  const [mappingEvidenceLoading, setMappingEvidenceLoading] = useState(false);
  const [mappingEvidenceError, setMappingEvidenceError] = useState<string | null>(null);

  // HSLOC reference codes, the facility's live configured mappings, and in-progress "+ Add Mapping"
  // selections for the HSLOC mapping modal's unmapped values, keyed by the unmapped source code.
  // Mappings are fetched fresh via getHslocMappings() -- the same call HslocStep itself makes --
  // rather than read off draft.hsloc.mappings: the draft only picks up HslocStep's edits when that
  // step's own Continue button patches it, so it can lag behind what saveHslocMappings actually
  // persisted (e.g. right after this modal's own "+ Add Mapping" adds one). Location Org / Encounter
  // have no equivalent -- unlike saveHslocMappings, neither has a save call that isn't gated to its
  // own onboarding step (see saveDraft's doc comment), so those two modals stay read-only and link
  // to the real step instead.
  const [hslocCodes, setHslocCodes] = useState<HslocCode[]>([]);
  const [hslocMappings, setHslocMappings] = useState<HslocMapping[]>([]);
  const [hslocDataLoading, setHslocDataLoading] = useState(false);
  const [hslocSelections, setHslocSelections] = useState<Record<string, string>>({});
  const [addingHslocCode, setAddingHslocCode] = useState<string | null>(null);

  const [queryPlanOpen, setQueryPlanOpen] = useState(false);
  const [queryPlan, setQueryPlan] = useState<QueryPlan | null>(null);
  const [queryPlanLoading, setQueryPlanLoading] = useState(false);
  const [queryPlanError, setQueryPlanError] = useState<string | null>(null);

  const [acquisitionLogOpen, setAcquisitionLogOpen] = useState(false);
  const [acquisitionLogs, setAcquisitionLogs] = useState<AcquisitionLogEntry[]>([]);
  const [acquisitionLogLoading, setAcquisitionLogLoading] = useState(false);
  const [acquisitionLogError, setAcquisitionLogError] = useState<string | null>(null);
  const [acquisitionLogFilters, setAcquisitionLogFilters] = useState<AcquisitionLogFilters>(EMPTY_ACQUISITION_LOG_FILTERS);

  const [exporting, setExporting] = useState(false);
  const [downloadingPatientId, setDownloadingPatientId] = useState<string | null>(null);

  async function handleViewQueryPlan() {
    if (!detail) {
      return;
    }
    setQueryPlanOpen(true);
    setQueryPlanLoading(true);
    setQueryPlanError(null);
    setQueryPlan(null);
    try {
      setQueryPlan(await api.getQueryPlan(detail.reportId));
    } catch (cause) {
      // A 404 means the facility simply has no query plan configured yet -- expected, not a
      // failure. Leave queryPlan at null so the "no plan configured" empty state renders instead
      // of the raw HTTP error.
      if (!(cause instanceof HttpError && cause.status === 404)) {
        setQueryPlanError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
      }
    } finally {
      setQueryPlanLoading(false);
    }
  }

  async function handleViewAcquisitionLog() {
    if (!detail) {
      return;
    }
    setAcquisitionLogOpen(true);
    setAcquisitionLogFilters(EMPTY_ACQUISITION_LOG_FILTERS);
    setAcquisitionLogLoading(true);
    setAcquisitionLogError(null);
    try {
      setAcquisitionLogs(await api.getAcquisitionLogs(detail.reportId));
    } catch (cause) {
      setAcquisitionLogError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setAcquisitionLogLoading(false);
    }
  }

  function handleExportQueryPlan() {
    if (!queryPlan) {
      return;
    }
    const parsed = parseQueryPlan(queryPlan.planJson);
    if (!parsed) {
      return;
    }
    downloadBlob(buildXlsxBlob([buildQueryPlanSheet(parsed)]), `Query_Plan_${queryPlan.reportId}.xlsx`);
  }

  function handleExportAcquisitionLog(entries: AcquisitionLogEntry[]) {
    if (!detail) {
      return;
    }
    downloadBlob(buildXlsxBlob([buildAcquisitionLogSheet(entries)]), `Acquisition_Log_${detail.reportId}.xlsx`);
  }

  // Bundles every section the onboarding POC zips into separate files (mappings/report
  // details/acquisition log/query plan) into one .xlsx workbook instead -- see reportExport.ts.
  // Covers every measure and patient, not just whichever DQM tab happens to be on screen, and
  // fetches the acquisition log / query plan fresh rather than relying on those modals having
  // been opened first.
  async function handleExportSummary() {
    if (!detail) {
      return;
    }
    setExporting(true);
    try {
      const [acquisitionLog, plan] = await Promise.all([
        api.getAcquisitionLogs(detail.reportId).catch(() => [] as AcquisitionLogEntry[]),
        api.getQueryPlan(detail.reportId).catch(() => null)
      ]);

      const sheets: XlsxSheet[] = [buildReportSummarySheet(detail), buildSelectedMeasuresSheet(detail, reportResults.requestedMeasuresByReportId)];

      const patientSheet = buildPatientReportingStatusSheet(patients, t);
      if (patientSheet.rows.length > 0) {
        sheets.push(patientSheet);
      }

      sheets.push(buildAcquisitionLogSheet(acquisitionLog));

      const parsedPlan = plan ? parseQueryPlan(plan.planJson) : null;
      if (parsedPlan) {
        sheets.push(buildQueryPlanSheet(parsedPlan));
      }

      downloadBlob(buildXlsxBlob(sheets), `${detail.reportId}_Report_Summary.xlsx`);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setExporting(false);
    }
  }

  async function handleDownloadPatientReport(patientId: string, dqmName: string | undefined) {
    if (!detail || !dqmName) {
      return;
    }
    const reportType = REAL_REPORT_TYPE_BY_DQM_NAME[dqmName];
    if (!reportType) {
      return;
    }
    setDownloadingPatientId(patientId);
    try {
      const blob = await api.exportPatientReport(detail.reportId, patientId, reportType);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${patientId}_${dqmName.replace(/\s+/g, '_')}_report.ndjson`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setDownloadingPatientId(null);
    }
  }

  // Real evidence behind the Location Org / HSLOC / Encounter Mapping indicators, from Report's
  // per-patient detail operation. Fetched on demand rather than for every row up front.
  async function openMappingEvidence(column: 'locationOrg' | 'hsloc' | 'encounter', patientId: string) {
    if (!detail) {
      return;
    }
    setMappingEvidenceColumn(column);
    setMappingEvidencePatientId(patientId);
    setMappingEvidence(null);
    setMappingEvidenceError(null);
    setMappingEvidenceLoading(true);
    setHslocSelections({});
    try {
      const evidence = await api.getPatientMappingEvidence(detail.reportId, patientId);
      setMappingEvidence(evidence);
    } catch (cause) {
      setMappingEvidenceError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setMappingEvidenceLoading(false);
    }
    if (column === 'hsloc') {
      setHslocDataLoading(true);
      try {
        // Mappings are always refetched (unlike the reference code list, cached once loaded) --
        // they can change between openings, including from this same modal's own "+ Add Mapping".
        const [codes, mappings] = await Promise.all([
          hslocCodes.length === 0 ? api.getHslocCodes() : Promise.resolve(hslocCodes),
          api.getHslocMappings()
        ]);
        setHslocCodes(codes);
        setHslocMappings(mappings);
      } catch (cause) {
        notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
      } finally {
        setHslocDataLoading(false);
      }
    }
  }

  // The only one of the three mapping indicators with a save call that isn't gated to its own
  // onboarding step (see saveHslocMappings vs. the note on ApiClient.saveDraft) -- so it is the only
  // one this screen can persist a new mapping through. Appends to the live hslocMappings (fetched
  // fresh when the modal opened, not draft.hsloc.mappings -- see the state comment above) rather
  // than replacing, mirroring the onboarding POC's "add to configuration" behavior. Also mirrors the
  // full result into the draft so the Location Identification step reflects it without a refetch.
  async function handleAddHslocMapping(unmappedCode: string) {
    const hslocCode = hslocSelections[unmappedCode];
    if (!hslocCode) {
      return;
    }
    setAddingHslocCode(unmappedCode);
    try {
      const nextMappings = [...hslocMappings, {sourceCode: unmappedCode, hslocCode}];
      await api.saveHslocMappings(nextMappings);
      setHslocMappings(nextMappings);
      patch('hsloc', {mappings: nextMappings});
      notifySuccess(t('onboarding:reportResults.detail.mappingEvidence.hslocMappingAdded'));
      setHslocSelections(prev => {
        const next = {...prev};
        delete next[unmappedCode];
        return next;
      });
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setAddingHslocCode(null);
    }
  }

  const loadReports = useCallback(async () => {
    try {
      const page = await api.listReports({page: 1, pageSize: 50});
      setReports(page.items);
      setLoadError(null);
      return true;
    } catch (cause) {
      setReports([]);
      setLoadError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
      return false;
    }
  }, [api, t]);

  useEffect(() => {
    setLoading(true);
    loadReports().finally(() => setLoading(false));
  }, [loadReports]);

  const loadDetail = useCallback(
    async (reportId: string) => {
      try {
        const [found, foundPatients] = await Promise.all([api.getReport(reportId), api.getReportPatients(reportId)]);
        setDetail(found);
        setPatients(foundPatients);
        setDetailError(null);
        return true;
      } catch (cause) {
        setDetailError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.detailLoadError'));
        return false;
      }
    },
    [api, t]
  );

  useEffect(() => {
    if (!viewingDetail || !viewingReportId) {
      return;
    }
    setDetailLoading(true);
    loadDetail(viewingReportId).finally(() => setDetailLoading(false));
  }, [viewingDetail, viewingReportId, loadDetail]);

  async function handleRefresh() {
    setRefreshing(true);
    const ok = await loadReports();
    setRefreshing(false);
    if (ok) {
      notifySuccess(t('onboarding:reportResults.messages.refreshed'));
    } else {
      notifyError(t('onboarding:reportResults.messages.loadError'));
    }
  }

  async function handleRefreshDetail() {
    if (!viewingReportId) {
      return;
    }
    setDetailLoading(true);
    const ok = await loadDetail(viewingReportId);
    setDetailLoading(false);
    if (ok) {
      notifySuccess(t('onboarding:reportResults.messages.refreshed'));
    } else {
      notifyError(t('onboarding:reportResults.messages.detailLoadError'));
    }
  }

  function handleGenerateNew() {
    if (reports.some(report => report.status === 'Pending')) {
      notifyError(t('onboarding:reportResults.messages.reportPending'));
      return;
    }
    goTo('report');
  }

  // The onboarding POC always makes the report id clickable, regardless of status -- a Pending or
  // Failed report still has whatever detail Report has recorded for it so far.
  function handleSelectReport(report: ReportSummary) {
    setActiveDqm(undefined);
    setSelectedPatientRow(null);
    patch('reportResults', {viewingReportId: report.reportId, latestStatus: report.status});
    openView({stepId: 'report-results', view: 'detail', params: {reportId: report.reportId}});
  }

  function handleDownloadUnavailable() {
    notifyError(t('onboarding:reportResults.detail.downloadUnavailable'));
  }

  // No service owns a "report accuracy acknowledgement" concept -- confirmed against Tenant,
  // Report and DMRP, and the onboarding POC never calls out for this either (it is a plain
  // `facility.reportResultsAcknowledged` flag there). Persisted through the draft save like any
  // other onboarding field, not a report-scoped API call.
  function handleAckChange(checked: boolean) {
    patch('reportResults', {accuracyAcknowledged: checked});
    setValidationMessage(null);
  }

  function handleNext() {
    if (!reportResults.accuracyAcknowledged) {
      setValidationMessage(t('onboarding:reportResults.messages.notAcknowledged'));
      return;
    }
    setValidationMessage(null);
    onNext();
  }

  if (viewingDetail) {
    const friendlyDetailMeasures = detail
      ? friendlyMeasuresFor(detail.measures, detail.reportId, reportResults.requestedMeasuresByReportId)
      : [];
    const dqmOptions = Array.from(new Set(friendlyDetailMeasures.map(measure => DIGITAL_QUALITY_MEASURE_BY_MEASURE[measure]).filter(Boolean)));
    const currentDqm = activeDqm && dqmOptions.includes(activeDqm) ? activeDqm : dqmOptions[0];
    const reportIsComplete = detail ? demoDisplayStatus(detail.status, detail.reportId) === 'Complete' : false;
    // Population narrows to patients with a measure report for the active dQM -- real per-dQM
    // data. The status pie is still each patient's one overall ReportingStatus: Link has no
    // per-dQM validation outcome to split it by.
    const dqmScopedPatients = patientsForDqm(patients, currentDqm).map(patient => ({
      ...patient,
      reportingStatus: demoDisplayReportingStatus(patient.reportingStatus, patient.patientId, reportIsComplete)
    }));
    const statusBreakdown = buildReportStatusBreakdown(dqmScopedPatients);
    const patientRows = toPatientRows(dqmScopedPatients, currentDqm);
    const mappingEvidencePatientRow = patientRows.find(row => row.patientId === mappingEvidencePatientId) ?? null;
    const locationOrgConfig = locationOrgConfigInfo(draft.locationOrg, t);
    const hslocUnmappedCodes = mappingEvidence
      ? Array.from(new Set(mappingEvidence.codeMaps.filter(isHslocCodeMap).flatMap(codeMap => codeMap.unmappedCodes)))
      : [];
    const encounterCodeMaps = mappingEvidence ? mappingEvidence.codeMaps.filter(codeMap => !isHslocCodeMap(codeMap)) : [];
    const encounterGroups = buildGroups(draft.encounter.codeSystems ?? [], draft.encounter.mappings ?? []);

    let sliceStart = 0;
    const pieSlices = statusBreakdown.map(slice => {
      const sweep = (slice.percent / 100) * 360;
      const path = describePieSlice(60, 60, 60, sliceStart, sliceStart + sweep);
      sliceStart += sweep;
      return {...slice, path};
    });

    const resourceBreakdown = selectedPatientRow ? buildResourceBreakdown(selectedPatientRow.resourceCountsByType) : [];
    let resourceSliceStart = 0;
    const resourcePieSlices = resourceBreakdown.map(slice => {
      const sweep = (slice.percent / 100) * 360;
      const path = describePieSlice(60, 60, 60, resourceSliceStart, resourceSliceStart + sweep);
      resourceSliceStart += sweep;
      return {...slice, path};
    });

    const parsedQueryPlan = queryPlan ? parseQueryPlan(queryPlan.planJson) : null;

    // Filter options are derived from whatever the report actually returned, not a fixed list --
    // a report with no Location queries simply shows no "Location" option, matching the data.
    const acquisitionResourceOptions = Array.from(new Set(acquisitionLogs.map(entry => entry.resource))).filter(Boolean).sort();
    const acquisitionPhaseOptions = Array.from(new Set(acquisitionLogs.map(entry => entry.queryPhase))).filter(Boolean).sort();
    const acquisitionTypeOptions = Array.from(new Set(acquisitionLogs.map(entry => entry.queryType).filter((value): value is string => Boolean(value)))).sort();
    const acquisitionStatusOptions = Array.from(new Set(acquisitionLogs.map(entry => entry.status))).filter(Boolean).sort();
    const filteredAcquisitionLogs = acquisitionLogs.filter(entry => {
      if (acquisitionLogFilters.patientId && !entry.patientId.toLowerCase().includes(acquisitionLogFilters.patientId.toLowerCase())) {
        return false;
      }
      if (acquisitionLogFilters.resource && entry.resource !== acquisitionLogFilters.resource) {
        return false;
      }
      if (acquisitionLogFilters.queryPhase && entry.queryPhase !== acquisitionLogFilters.queryPhase) {
        return false;
      }
      if (acquisitionLogFilters.queryType && entry.queryType !== acquisitionLogFilters.queryType) {
        return false;
      }
      if (acquisitionLogFilters.status && entry.status !== acquisitionLogFilters.status) {
        return false;
      }
      return true;
    });

    return (
      <div className="nhsn-link__content nhsn-link__report-results">
        <div className="nhsn-link__report-results-detail-header">
          <PageHeader title={t('onboarding:reportResults.detail.title')} />
          <div className="nhsn-link__report-results-detail-header-actions">
            <button
              type="button"
              className="nhsn-link__report-results-icon-button"
              onClick={handleRefreshDetail}
              disabled={detailLoading}
              aria-label={t('common:actions.refresh')}>
              <RefreshIcon />
            </button>
          </div>
        </div>

        {detailLoading && <NHSNLoadingIndicator />}

        {!detailLoading && detailError && (
          <MessageContainer type="error" showIcon>
            <span role="alert">{detailError}</span>
          </MessageContainer>
        )}

        {!detailLoading && !detailError && detail && (
          <>
            <dl className="nhsn-link__report-results-detail-list">
              <div>
                <dt>{t('onboarding:reportResults.columns.reportId')}</dt>
                <dd>{detail.reportId}</dd>
              </div>
              <div>
                <dt>{t('onboarding:reportResults.detail.reportingPeriod')}</dt>
                <dd>
                  {t('onboarding:reportResults.detail.reportingPeriodValue', {
                    start: formatDate(detail.startDate),
                    end: formatDate(detail.endDate)
                  })}
                </dd>
              </div>
              <div>
                <dt>{t('onboarding:reportResults.columns.createDate')}</dt>
                <dd>{formatDateTime(detail.createDate)}</dd>
              </div>
              <div>
                <dt>{t('onboarding:reportResults.columns.patientCount')}</dt>
                <dd>{detail.patientCount}</dd>
              </div>
              <div>
                <dt>{t('onboarding:reportResults.columns.status')}</dt>
                <dd>
                  <span className={`nhsn-link__status-pill ${STATUS_PILL_CLASS[demoDisplayStatus(detail.status, detail.reportId)]}`}>
                    {t(`onboarding:reportResults.status.${demoDisplayStatus(detail.status, detail.reportId)}`)}
                  </span>
                </dd>
              </div>
            </dl>

            <h3 className="nhsn-link__report-results-detail-section-title">
              {t('onboarding:reportResults.detail.selectedMeasures')}
            </h3>
            <div className="nhsn-link__report-results-table-scroll">
              <table className="nhsn-link__report-results-table">
                <thead>
                  <tr>
                    <th>{t('onboarding:reportResults.detail.columns.nhsnMeasure')}</th>
                    <th>{t('onboarding:reportResults.detail.columns.digitalQualityMeasure')}</th>
                  </tr>
                </thead>
                <tbody>
                  {friendlyDetailMeasures.map(measure => {
                    const tabName = DIGITAL_QUALITY_MEASURE_BY_MEASURE[measure];
                    const realDqmId = tabName ? REAL_REPORT_TYPE_BY_DQM_NAME[tabName] : undefined;
                    const dqmInfo = realDqmId ? DQM_INFO_BY_REAL_ID[realDqmId] : undefined;
                    return (
                      <tr key={measure}>
                        <td>{measure}</td>
                        <td>
                          {dqmInfo ? (
                            <a
                              className="nhsn-link__report-results-link"
                              href={dqmInfo.url}
                              target="_blank"
                              rel="noopener noreferrer">
                              {dqmInfo.name}
                            </a>
                          ) : (
                            '—'
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {dqmOptions.length > 0 && (
              <Tabs
                tabs={dqmOptions.map(dqm => ({id: dqm, label: dqm}))}
                activeTab={currentDqm}
                onTabChange={setActiveDqm}
                label={t('onboarding:reportResults.detail.dqmTabsLabel')}
              />
            )}

            <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.reportStatus')}</h3>
            {statusBreakdown.length > 0 && (
              <div className="nhsn-link__report-results-chart-row">
                <svg width="120" height="120" viewBox="0 0 120 120" role="img" aria-label={t('onboarding:reportResults.detail.reportStatus')}>
                  {pieSlices.map(slice => (
                    <path key={slice.labelKey} d={slice.path} fill={slice.color} />
                  ))}
                </svg>
                <ul className="nhsn-link__report-results-legend">
                  {statusBreakdown.map(slice => (
                    <li key={slice.labelKey}>
                      <span className="nhsn-link__report-results-legend-dot" style={{background: slice.color}} />
                      <span className="nhsn-link__report-results-legend-label">
                        {t(`onboarding:reportResults.detail.statusCategories.${slice.labelKey}`)}
                      </span>
                      <span className="nhsn-link__report-results-legend-count">
                        {slice.count} ({slice.percent}%)
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {patientRows.length > 0 && (
              <>
                <h3 className="nhsn-link__report-results-detail-section-title">
                  {t('onboarding:reportResults.detail.patientReportingStatus', {count: dqmScopedPatients.length})}
                </h3>
                <div className="nhsn-link__report-results-table-scroll">
                  <table className="nhsn-link__report-results-table nhsn-link__report-results-table--light-border nhsn-link__report-results-table--fixed">
                    <colgroup>
                      <col style={{width: '9%'}} />
                      <col style={{width: '12%'}} />
                      <col style={{width: '18%'}} />
                      <col style={{width: '11%'}} />
                      <col style={{width: '11%'}} />
                      <col style={{width: '13%'}} />
                      <col style={{width: '13%'}} />
                      <col style={{width: '13%'}} />
                    </colgroup>
                    <thead>
                      <tr>
                        <th>{t('onboarding:reportResults.detail.columns.patientId')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.fhirResourceCount')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.reportStatus')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.preQualResults')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.locationOrg')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.hslocMapping')}</th>
                        <th>{t('onboarding:reportResults.detail.columns.encounterMapping')}</th>
                        <th aria-hidden="true" />
                      </tr>
                    </thead>
                    <tbody>
                      {patientRows.map(row => (
                        <tr key={row.patientId}>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__report-results-link"
                              onClick={() => setSelectedPatientRow(row)}>
                              {row.patientId}
                            </button>
                          </td>
                          <td>{row.fhirResourceCount}</td>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__status-pill-button"
                              onClick={() => setTimelinePatientRow(row)}
                              aria-label={t('onboarding:reportResults.detail.patientTimeline.title')}
                              title={t('onboarding:reportResults.detail.patientTimeline.title')}>
                              <span className={`nhsn-link__status-pill ${STATUS_PILL_CLASS_BY_KEY[row.reportStatusKey]}`}>
                                {t(`onboarding:reportResults.detail.statusCategories.${row.reportStatusKey}`)}
                              </span>
                            </button>
                          </td>
                          <td>
                            {row.hasPreQualResults ? (
                              <button
                                type="button"
                                className="nhsn-link__report-results-icon-button"
                                onClick={() => setPreQualPatientRow(row)}
                                aria-label={t('onboarding:reportResults.detail.columns.preQualResults')}
                                title={t('onboarding:reportResults.detail.columns.preQualResults')}>
                                <ChartIcon />
                              </button>
                            ) : (
                              t('onboarding:reportResults.detail.notApplicable')
                            )}
                          </td>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__status-pill-button"
                              onClick={() => openMappingEvidence('locationOrg', row.patientId)}>
                              <span className={`nhsn-link__mapping-pill ${row.locationOrgFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                                {row.locationOrgFound
                                  ? t('onboarding:reportResults.detail.mapping.found')
                                  : t('onboarding:reportResults.detail.mapping.notFound')}
                              </span>
                            </button>
                          </td>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__status-pill-button"
                              onClick={() => openMappingEvidence('hsloc', row.patientId)}>
                              <span className={`nhsn-link__mapping-pill ${row.hslocFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                                {row.hslocFound
                                  ? t('onboarding:reportResults.detail.mapping.found')
                                  : t('onboarding:reportResults.detail.mapping.notFound')}
                              </span>
                            </button>
                          </td>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__status-pill-button"
                              onClick={() => openMappingEvidence('encounter', row.patientId)}>
                              <span className={`nhsn-link__mapping-pill ${row.encounterFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                                {row.encounterFound
                                  ? t('onboarding:reportResults.detail.mapping.found')
                                  : t('onboarding:reportResults.detail.mapping.notFound')}
                              </span>
                            </button>
                          </td>
                          <td>
                            <button
                              type="button"
                              className="nhsn-link__report-results-icon-button"
                              onClick={() => handleDownloadPatientReport(row.patientId, currentDqm)}
                              disabled={downloadingPatientId === row.patientId}
                              aria-label={t('onboarding:reportResults.detail.downloadPatientReport')}
                              title={t('onboarding:reportResults.detail.downloadPatientReport')}>
                              <DownloadIcon />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </>
        )}

        <StepActions saving={saving}>
          <Button variant="secondary" onClick={closeView}>
            {t('common:actions.back')}
          </Button>
          <Button variant="secondary" onClick={handleViewQueryPlan}>
            {t('onboarding:reportResults.detail.actions.viewQueryPlan')}
          </Button>
          <Button variant="secondary" onClick={handleViewAcquisitionLog}>
            {t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}
          </Button>
          <Button variant="secondary" onClick={handleExportSummary} loading={exporting} disabled={exporting}>
            <DownloadIcon />
            {t('onboarding:reportResults.detail.actions.exportSummary')}
          </Button>
        </StepActions>

        <Modal
          open={queryPlanOpen}
          title={t('onboarding:reportResults.detail.actions.viewQueryPlan')}
          onClose={() => setQueryPlanOpen(false)}
          size="large"
          footer={
            <>
              {parsedQueryPlan && (
                <Button variant="secondary" onClick={handleExportQueryPlan}>
                  <DownloadIcon />
                  {t('onboarding:reportResults.detail.actions.exportToExcel')}
                </Button>
              )}
              <Button variant="secondary" onClick={() => setQueryPlanOpen(false)}>
                {t('common:actions.close')}
              </Button>
            </>
          }>
          {queryPlanLoading && <NHSNLoadingIndicator />}
          {!queryPlanLoading && queryPlanError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{queryPlanError}</span>
            </MessageContainer>
          )}
          {!queryPlanLoading && !queryPlanError && parsedQueryPlan && (
            <>
              <ul className="nhsn-link__summary-list">
                <li>
                  <span>{t('onboarding:reportResults.detail.queryPlan.ehrType')}</span>
                  <span>{parsedQueryPlan.ehrDescription ?? '—'}</span>
                </li>
              </ul>

              <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.queryPlan.planDetails')}</h3>
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <tbody>
                    <tr>
                      <td>{t('onboarding:reportResults.detail.queryPlan.planName')}</td>
                      <td>{parsedQueryPlan.planName ?? '—'}</td>
                    </tr>
                    <tr>
                      <td>{t('onboarding:reportResults.detail.queryPlan.lookBack')}</td>
                      <td>{parsedQueryPlan.lookBack ?? '—'}</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <h3 className="nhsn-link__report-results-detail-section-title">{t('onboarding:reportResults.detail.queryPlan.queries')}</h3>
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <thead>
                    <tr>
                      <th>{t('onboarding:reportResults.detail.queryPlan.section')}</th>
                      <th>{t('onboarding:reportResults.detail.queryPlan.resourceType')}</th>
                      <th>{t('onboarding:reportResults.detail.queryPlan.queryType')}</th>
                      <th>{t('onboarding:reportResults.detail.queryPlan.operationType')}</th>
                      <th>{t('onboarding:reportResults.detail.queryPlan.paged')}</th>
                      <th>{t('onboarding:reportResults.detail.queryPlan.parameters')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[
                      ...parsedQueryPlan.initialQueries.map(query => ({section: 'Initial', query})),
                      ...parsedQueryPlan.supplementalQueries.map(query => ({section: 'Supplemental', query}))
                    ].map(({section, query}, index) => (
                      <tr key={`${section}-${query.resourceType}-${index}`}>
                        <td>{section}</td>
                        <td>{query.resourceType}</td>
                        <td>{query.queryConfigType ?? '—'}</td>
                        <td>{query.operationType ?? '—'}</td>
                        <td>{query.paged === undefined ? '—' : query.paged ? t('common:actions.yes') : t('common:actions.no')}</td>
                        <td>{query.parameters.length > 0 ? query.parameters.join(', ') : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
          {!queryPlanLoading && !queryPlanError && !parsedQueryPlan && (
            queryPlan ? (
              <pre className="nhsn-link__report-results-json">{queryPlan.planJson}</pre>
            ) : (
              <p>{t('onboarding:reportResults.detail.queryPlan.empty')}</p>
            )
          )}
        </Modal>

        <Modal
          open={acquisitionLogOpen}
          title={t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}
          onClose={() => setAcquisitionLogOpen(false)}
          size="large"
          footer={
            <>
              {acquisitionLogs.length > 0 && (
                <Button variant="secondary" onClick={() => handleExportAcquisitionLog(filteredAcquisitionLogs)}>
                  <DownloadIcon />
                  {t('onboarding:reportResults.detail.actions.exportToExcel')}
                </Button>
              )}
              <Button variant="secondary" onClick={() => setAcquisitionLogOpen(false)}>
                {t('common:actions.close')}
              </Button>
            </>
          }>
          {acquisitionLogLoading && <NHSNLoadingIndicator />}
          {!acquisitionLogLoading && acquisitionLogError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{acquisitionLogError}</span>
            </MessageContainer>
          )}
          {!acquisitionLogLoading && !acquisitionLogError && (
            acquisitionLogs.length > 0 ? (
              <>
                <div className="nhsn-link__report-results-filters">
                  <TextField
                    id="acquisition-log-filter-patient-id"
                    label={t('onboarding:reportResults.detail.columns.patientId')}
                    placeholder={t('onboarding:reportResults.detail.acquisitionLog.filterPatientPlaceholder')}
                    value={acquisitionLogFilters.patientId}
                    onChange={value => setAcquisitionLogFilters(prev => ({...prev, patientId: value}))}
                  />
                  <Select
                    id="acquisition-log-filter-resource"
                    label={t('onboarding:reportResults.detail.acquisitionLog.resource')}
                    placeholder={t('onboarding:reportResults.detail.acquisitionLog.allResources')}
                    options={acquisitionResourceOptions.map(value => ({value, label: value}))}
                    value={acquisitionLogFilters.resource}
                    onChange={value => setAcquisitionLogFilters(prev => ({...prev, resource: value}))}
                  />
                  <Select
                    id="acquisition-log-filter-phase"
                    label={t('onboarding:reportResults.detail.acquisitionLog.queryPhase')}
                    placeholder={t('onboarding:reportResults.detail.acquisitionLog.allPhases')}
                    options={acquisitionPhaseOptions.map(value => ({value, label: value}))}
                    value={acquisitionLogFilters.queryPhase}
                    onChange={value => setAcquisitionLogFilters(prev => ({...prev, queryPhase: value}))}
                  />
                  <Select
                    id="acquisition-log-filter-type"
                    label={t('onboarding:reportResults.detail.acquisitionLog.queryType')}
                    placeholder={t('onboarding:reportResults.detail.acquisitionLog.allTypes')}
                    options={acquisitionTypeOptions.map(value => ({value, label: value}))}
                    value={acquisitionLogFilters.queryType}
                    onChange={value => setAcquisitionLogFilters(prev => ({...prev, queryType: value}))}
                  />
                  <Select
                    id="acquisition-log-filter-status"
                    label={t('onboarding:reportResults.detail.acquisitionLog.status')}
                    placeholder={t('onboarding:reportResults.detail.acquisitionLog.allStatuses')}
                    options={acquisitionStatusOptions.map(value => ({value, label: value}))}
                    value={acquisitionLogFilters.status}
                    onChange={value => setAcquisitionLogFilters(prev => ({...prev, status: value}))}
                  />
                </div>
                <p className="nhsn-link__report-results-result-count">
                  {t('onboarding:reportResults.detail.acquisitionLog.resultCount', {
                    shown: filteredAcquisitionLogs.length,
                    total: acquisitionLogs.length
                  })}
                </p>
                <div className="nhsn-link__report-results-table-scroll">
                  <table className="nhsn-link__report-results-table">
                    <thead>
                      <tr>
                        <th>{t('onboarding:reportResults.detail.columns.patientId')}</th>
                        <th>{t('onboarding:reportResults.detail.acquisitionLog.resource')}</th>
                        <th>{t('onboarding:reportResults.detail.acquisitionLog.queryPhase')}</th>
                        <th>{t('onboarding:reportResults.detail.acquisitionLog.queryType')}</th>
                        <th>{t('onboarding:reportResults.detail.acquisitionLog.parameters')}</th>
                        <th>{t('onboarding:reportResults.detail.acquisitionLog.status')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredAcquisitionLogs.map((entry, index) => (
                        <tr key={`${entry.patientId}-${entry.resource}-${index}`}>
                          <td>{entry.patientId}</td>
                          <td>{entry.resource}</td>
                          <td>{entry.queryPhase}</td>
                          <td>{entry.queryType ?? '—'}</td>
                          <td>{entry.parameters.length > 0 ? entry.parameters.join(', ') : '—'}</td>
                          <td>{entry.status}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            ) : (
              <p>{t('onboarding:reportResults.detail.acquisitionLog.empty')}</p>
            )
          )}
        </Modal>

        <Modal
          open={Boolean(selectedPatientRow)}
          title={t('onboarding:reportResults.detail.patientDetail.title')}
          onClose={() => setSelectedPatientRow(null)}
          size="small"
          footer={
            <Button variant="secondary" onClick={() => setSelectedPatientRow(null)}>
              {t('common:actions.close')}
            </Button>
          }>
          {selectedPatientRow && detail && (
            <>
              <dl className="nhsn-link__report-results-detail-list">
                <div>
                  <dt>{t('onboarding:reportResults.detail.columns.patientId')}</dt>
                  <dd>{selectedPatientRow.patientId}</dd>
                </div>
                <div>
                  <dt>{t('onboarding:reportResults.columns.reportId')}</dt>
                  <dd>{detail.reportId}</dd>
                </div>
                <div>
                  <dt>{t('onboarding:reportResults.detail.patientDetail.measure')}</dt>
                  <dd>{currentDqm ?? '—'}</dd>
                </div>
                <div>
                  <dt>{t('onboarding:reportResults.detail.patientDetail.reportingStatus')}</dt>
                  <dd>
                    <span className={`nhsn-link__status-pill ${STATUS_PILL_CLASS_BY_KEY[selectedPatientRow.reportStatusKey]}`}>
                      {t(`onboarding:reportResults.detail.statusCategories.${selectedPatientRow.reportStatusKey}`)}
                    </span>
                  </dd>
                </div>
              </dl>

              <h3 className="nhsn-link__report-results-detail-section-title">
                {t('onboarding:reportResults.detail.patientDetail.fhirResourceCount')}
              </h3>
              {resourceBreakdown.length > 0 && (
                <div className="nhsn-link__report-results-chart-row">
                  <svg
                    width="120"
                    height="120"
                    viewBox="0 0 120 120"
                    role="img"
                    aria-label={t('onboarding:reportResults.detail.patientDetail.fhirResourceCount')}>
                    {resourcePieSlices.map(slice => (
                      <path key={slice.labelKey} d={slice.path} fill={slice.color} />
                    ))}
                  </svg>
                  <ul className="nhsn-link__report-results-legend">
                    {resourceBreakdown.map(slice => (
                      <li key={slice.labelKey}>
                        <span className="nhsn-link__report-results-legend-dot" style={{background: slice.color}} />
                        {/* Resource type is a FHIR resource type name as Report returns it -- data, not UI copy. */}
                        <span className="nhsn-link__report-results-legend-label">{slice.labelKey}</span>
                        <span className="nhsn-link__report-results-legend-count">
                          {slice.count} ({slice.percent}%)
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              <div className="nhsn-link__report-results-patient-detail-actions">
                <Button variant="secondary" onClick={handleDownloadUnavailable}>
                  <DownloadIcon />
                  {t('onboarding:reportResults.detail.patientDetail.downloadResourceBundle')}
                </Button>
                <Button variant="secondary" onClick={handleDownloadUnavailable}>
                  <DownloadIcon />
                  {t('onboarding:reportResults.detail.patientDetail.downloadReport')}
                </Button>
              </div>
            </>
          )}
        </Modal>

        {timelinePatientRow && (
          <PatientStatusTimelineModal
            open
            onClose={() => setTimelinePatientRow(null)}
            patientId={timelinePatientRow.patientId}
            measureName={currentDqm}
            reportingStatus={timelinePatientRow.reportingStatus}
          />
        )}

        {preQualPatientRow && detail && (
          <PreQualResultsModal
            open
            onClose={() => setPreQualPatientRow(null)}
            patientId={preQualPatientRow.patientId}
            measureName={currentDqm}
            reportingStatus={preQualPatientRow.reportingStatus}
            reportId={detail.reportId}
          />
        )}

        {/* Location Org Mapping -- read-only: the facility's configured mappings plus the real
            per-patient evidence Report recorded. Unlike HSLOC below, there is no save call for
            Location Org config that isn't gated to the Organization Identification step itself
            (see ApiClient.saveDraft), so "fixing" a miss here just links there instead of faking
            an inline edit that wouldn't persist. */}
        <Modal
          open={mappingEvidenceColumn === 'locationOrg'}
          title={t('onboarding:reportResults.detail.mappingEvidence.locationOrgTitle')}
          onClose={() => setMappingEvidenceColumn(null)}
          size="large"
          footer={
            <Button variant="secondary" onClick={() => setMappingEvidenceColumn(null)}>
              {t('common:actions.close')}
            </Button>
          }>
          <dl className="nhsn-link__report-results-detail-list">
            <div>
              <dt>{t('onboarding:reportResults.detail.columns.patientId')}</dt>
              <dd>{mappingEvidencePatientId}</dd>
            </div>
            <div>
              <dt>{t('onboarding:reportResults.detail.mappingEvidence.resolutionMethod')}</dt>
              <dd>{locationOrgConfig ? t(METHOD_LABEL_KEYS[locationOrgConfig.method]) : t('onboarding:reportResults.detail.notApplicable')}</dd>
            </div>
          </dl>

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.mappingEvidence.configuredLocationOrgMappings')}
          </h3>
          {locationOrgConfig ? (
            <div className="nhsn-link__report-results-table-scroll">
              <table className="nhsn-link__report-results-table">
                <thead>
                  <tr>
                    {locationOrgConfig.headers.map(header => (
                      <th key={header}>{header}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {locationOrgConfig.rows.length === 0 ? (
                    <tr>
                      <td colSpan={locationOrgConfig.headers.length}>
                        {t('onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings')}
                      </td>
                    </tr>
                  ) : (
                    locationOrgConfig.rows.map((cells, index) => (
                      <tr key={index}>
                        {cells.map((cell, cellIndex) => (
                          <td key={cellIndex}>{cell || '—'}</td>
                        ))}
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="nhsn-link__hint-text">{t('onboarding:reportResults.detail.mappingEvidence.noStructuredMethod')}</p>
          )}

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.mappingEvidence.locationEvidenceHeading')}
          </h3>
          {mappingEvidenceLoading && <NHSNLoadingIndicator />}
          {!mappingEvidenceLoading && mappingEvidenceError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{mappingEvidenceError}</span>
            </MessageContainer>
          )}
          {!mappingEvidenceLoading && !mappingEvidenceError && mappingEvidence && (
            mappingEvidence.locationOrg && mappingEvidence.locationOrg.matches.length > 0 ? (
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <thead>
                    <tr>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.locationId')}</th>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.locationAlias')}</th>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.isOrgLocation')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {mappingEvidence.locationOrg.matches.map((match, index) => (
                      <tr key={`${match.locationId}-${index}`}>
                        <td>{match.locationId}</td>
                        <td>{match.locationAlias ?? match.locationName ?? '—'}</td>
                        <td>
                          <span className={`nhsn-link__mapping-pill ${match.isOrgLocation ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                            {match.isOrgLocation
                              ? t('onboarding:reportResults.detail.mapping.found')
                              : t('onboarding:reportResults.detail.mapping.notFound')}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p>{t('onboarding:reportResults.detail.mappingEvidence.noEvidence')}</p>
            )
          )}

          {mappingEvidencePatientRow && !mappingEvidencePatientRow.locationOrgFound && (
            <MessageContainer type="info" showIcon>
              <p>{t('onboarding:reportResults.detail.mappingEvidence.notFoundLocationOrgHint')}</p>
              <Button
                variant="secondary"
                onClick={() => {
                  setMappingEvidenceColumn(null);
                  goTo('location-org');
                }}>
                {t('onboarding:reportResults.detail.mappingEvidence.goToLocationOrg')}
              </Button>
            </MessageContainer>
          )}
        </Modal>

        {/* HSLOC Mapping -- the one mapping type this screen can actually fix: saveHslocMappings is
            a standalone call, not gated to the Location Identification step the way saveDraft is
            (see handleAddHslocMapping above), so an unmapped value acquired for this patient gets a
            real "+ Add Mapping" control. */}
        <Modal
          open={mappingEvidenceColumn === 'hsloc'}
          title={t('onboarding:reportResults.detail.mappingEvidence.hslocTitle')}
          onClose={() => setMappingEvidenceColumn(null)}
          size="large"
          footer={
            <Button variant="secondary" onClick={() => setMappingEvidenceColumn(null)}>
              {t('common:actions.close')}
            </Button>
          }>
          <dl className="nhsn-link__report-results-detail-list">
            <div>
              <dt>{t('onboarding:reportResults.detail.columns.patientId')}</dt>
              <dd>{mappingEvidencePatientId}</dd>
            </div>
          </dl>

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.mappingEvidence.configuredHslocMappings')}
          </h3>
          <div className="nhsn-link__report-results-table-scroll">
            <table className="nhsn-link__report-results-table">
              <thead>
                <tr>
                  <th>{t('onboarding:reportResults.detail.mappingEvidence.yourCode')}</th>
                  <th>{vendorProfile?.hslocSourceLabel ?? t('onboarding:hsloc.mapping.fields.locationValueFallback')}</th>
                  <th>{t('onboarding:reportResults.detail.mappingEvidence.hslocCode')}</th>
                </tr>
              </thead>
              <tbody>
                {hslocDataLoading ? (
                  <tr>
                    <td colSpan={3}>
                      <NHSNLoadingIndicator />
                    </td>
                  </tr>
                ) : hslocMappings.length === 0 ? (
                  <tr>
                    <td colSpan={3}>{t('onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings')}</td>
                  </tr>
                ) : (
                  hslocMappings.map((mapping, index) => (
                    <tr key={`${mapping.sourceCode}-${index}`}>
                      <td>{mapping.sourceDisplay || '—'}</td>
                      <td>{mapping.sourceCode}</td>
                      <td>{mapping.hslocCode}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {mappingEvidenceLoading && <NHSNLoadingIndicator />}
          {!mappingEvidenceLoading && mappingEvidenceError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{mappingEvidenceError}</span>
            </MessageContainer>
          )}

          {!mappingEvidenceLoading && !mappingEvidenceError && hslocUnmappedCodes.length > 0 && (
            <>
              <h3 className="nhsn-link__report-results-detail-section-title">
                {t('onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading')}
              </h3>
              {hslocDataLoading ? (
                <NHSNLoadingIndicator />
              ) : (
                <div className="nhsn-link__report-results-table-scroll">
                  <table className="nhsn-link__report-results-table">
                    <thead>
                      <tr>
                        <th>{vendorProfile?.hslocSourceLabel ?? t('onboarding:hsloc.mapping.fields.locationValueFallback')}</th>
                        <th>{t('onboarding:reportResults.detail.mappingEvidence.hslocCode')}</th>
                        <th aria-hidden="true" />
                      </tr>
                    </thead>
                    <tbody>
                      {hslocUnmappedCodes.map(code => (
                        <tr key={code}>
                          <td>{code}</td>
                          <td>
                            <Select
                              id={`hsloc-add-${code}`}
                              label={t('onboarding:reportResults.detail.mappingEvidence.hslocCode')}
                              placeholder={t('onboarding:reportResults.detail.mappingEvidence.selectHslocCode')}
                              options={hslocCodes.map(hslocCode => ({value: hslocCode.code, label: `${hslocCode.code} - ${hslocCode.display}`}))}
                              value={hslocSelections[code] ?? ''}
                              onChange={value => setHslocSelections(prev => ({...prev, [code]: value}))}
                            />
                          </td>
                          <td>
                            <Button
                              variant="secondary"
                              onClick={() => handleAddHslocMapping(code)}
                              disabled={!hslocSelections[code] || addingHslocCode === code}
                              loading={addingHslocCode === code}>
                              {t('onboarding:reportResults.detail.mappingEvidence.addMapping')}
                            </Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <p className="nhsn-link__hint-text">{t('onboarding:reportResults.detail.mappingEvidence.hslocAddedHint')}</p>
            </>
          )}
        </Modal>

        {/* Encounter Mapping -- read-only, same reasoning as Location Org above: Encounter Mapping
            config only saves through the Encounter Mapping step's own saveDraft call. */}
        <Modal
          open={mappingEvidenceColumn === 'encounter'}
          title={t('onboarding:reportResults.detail.mappingEvidence.encounterTitle')}
          onClose={() => setMappingEvidenceColumn(null)}
          size="large"
          footer={
            <Button variant="secondary" onClick={() => setMappingEvidenceColumn(null)}>
              {t('common:actions.close')}
            </Button>
          }>
          <dl className="nhsn-link__report-results-detail-list">
            <div>
              <dt>{t('onboarding:reportResults.detail.columns.patientId')}</dt>
              <dd>{mappingEvidencePatientId}</dd>
            </div>
          </dl>

          {mappingEvidenceLoading && <NHSNLoadingIndicator />}
          {!mappingEvidenceLoading && mappingEvidenceError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{mappingEvidenceError}</span>
            </MessageContainer>
          )}

          {!mappingEvidenceLoading && !mappingEvidenceError && encounterCodeMaps.length > 0 && (
            <>
              <h3 className="nhsn-link__report-results-detail-section-title">
                {t('onboarding:reportResults.detail.mappingEvidence.acquiredEncounterValueHeading')}
              </h3>
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <thead>
                    <tr>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.sourceSystem')}</th>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.unmappedCodes')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {encounterCodeMaps.map((codeMap, index) => (
                      <tr key={`${codeMap.sourceSystem}-${index}`}>
                        <td>{codeMap.sourceSystem}</td>
                        <td>{codeMap.unmappedCodes.length > 0 ? codeMap.unmappedCodes.join(', ') : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.mappingEvidence.configuredEncounterCodeSystems')}
          </h3>
          {encounterGroups.length === 0 ? (
            <p className="nhsn-link__hint-text">{t('onboarding:reportResults.detail.mappingEvidence.noCodeSystemsConfigured')}</p>
          ) : (
            encounterGroups.map(group => (
              <div key={group.groupKey} className="nhsn-link__field-group">
                <h4 className="nhsn-link__report-results-detail-section-title">
                  {group.codeSystem || t('onboarding:reportResults.detail.mappingEvidence.codeSystem')}
                </h4>
                <div className="nhsn-link__report-results-table-scroll">
                  <table className="nhsn-link__report-results-table">
                    <thead>
                      <tr>
                        <th>{t('onboarding:reportResults.detail.mappingEvidence.localValue')}</th>
                        <th>{t('onboarding:reportResults.detail.mappingEvidence.standardSystem')}</th>
                        <th>{t('onboarding:reportResults.detail.mappingEvidence.standardCode')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {group.mappings.length === 0 ? (
                        <tr>
                          <td colSpan={3}>{t('onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings')}</td>
                        </tr>
                      ) : (
                        group.mappings.map(row => (
                          <tr key={row.rowKey}>
                            <td>{row.localValue}</td>
                            <td>{row.targetSystem || '—'}</td>
                            <td>{row.targetCode || '—'}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            ))
          )}

          {mappingEvidencePatientRow && !mappingEvidencePatientRow.encounterFound && (
            <MessageContainer type="info" showIcon>
              <p>{t('onboarding:reportResults.detail.mappingEvidence.notFoundEncounterHint')}</p>
              <Button
                variant="secondary"
                onClick={() => {
                  setMappingEvidenceColumn(null);
                  goTo('encounter');
                }}>
                {t('onboarding:reportResults.detail.mappingEvidence.goToEncounterMapping')}
              </Button>
            </MessageContainer>
          )}
        </Modal>
      </div>
    );
  }

  return (
    <div className="nhsn-link__content nhsn-link__report-results">
      <PageHeader title={t('onboarding:reportResults.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:reportResults.subtitle')}</p>

      <div className="nhsn-link__report-results-actions">
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={handleGenerateNew} disabled={saving || loading}>
          {t('onboarding:reportResults.actions.generateNewReport')}
        </Button>
        <Button variant="secondary" onClick={handleRefresh} disabled={saving || refreshing}>
          <RefreshIcon />
          {t('common:actions.refresh')}
        </Button>
      </div>

      {loading && <NHSNLoadingIndicator />}

      {!loading && loadError && reports.length === 0 && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{loadError}</span>
        </MessageContainer>
      )}

      {!loading && reports.length > 0 && (
        <div className="nhsn-link__report-results-table-scroll">
          <table className="nhsn-link__report-results-table nhsn-link__report-results-table--fixed">
            <colgroup>
              <col style={{width: '11%'}} />
              <col style={{width: '26%'}} />
              <col style={{width: '13%'}} />
              <col style={{width: '13%'}} />
              <col style={{width: '13%'}} />
              <col style={{width: '15%'}} />
              <col style={{width: '9%'}} />
            </colgroup>
            <thead>
              <tr>
                <th>{t('onboarding:reportResults.columns.reportId')}</th>
                <th>{t('onboarding:reportResults.columns.measures')}</th>
                <th>{t('onboarding:reportResults.columns.patientCount')}</th>
                <th>{t('onboarding:reportResults.columns.startDate')}</th>
                <th>{t('onboarding:reportResults.columns.endDate')}</th>
                <th>{t('onboarding:reportResults.columns.createDate')}</th>
                <th>{t('onboarding:reportResults.columns.status')}</th>
              </tr>
            </thead>
            <tbody>
              {reports.length === 0 ? (
                <tr>
                  <td className="nhsn-link__report-results-empty" colSpan={7}>
                    {t('onboarding:reportResults.messages.noReports')}
                  </td>
                </tr>
              ) : (
                reports.map(report => (
                  <tr key={report.reportId}>
                    <td className="nhsn-link__report-results-id" title={report.reportId}>
                      <button type="button" className="nhsn-link__report-results-link" onClick={() => handleSelectReport(report)}>
                        {report.reportId}
                      </button>
                    </td>
                    <td>
                      <span className="nhsn-link__report-results-measures">
                        {friendlyMeasuresFor(report.measures, report.reportId, reportResults.requestedMeasuresByReportId).map(measure => (
                          <span key={measure} className="nhsn-link__measure-badge" style={{background: measureColor(measure)}}>
                            {measure}
                          </span>
                        ))}
                      </span>
                    </td>
                    <td className="nhsn-link__report-results-nowrap">{report.patientCount}</td>
                    <td className="nhsn-link__report-results-nowrap">{formatDate(report.startDate)}</td>
                    <td className="nhsn-link__report-results-nowrap">{formatDate(report.endDate)}</td>
                    <td className="nhsn-link__report-results-nowrap">{formatDateTime(report.createDate)}</td>
                    <td className="nhsn-link__report-results-nowrap">
                      <span className={`nhsn-link__status-pill ${STATUS_PILL_CLASS[demoDisplayStatus(report.status, report.reportId)]}`}>
                        {t(`onboarding:reportResults.status.${demoDisplayStatus(report.status, report.reportId)}`)}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      <div className="nhsn-link__report-results-ack">
        <CheckboxField
          id="report-results-accuracy-ack"
          label={t('onboarding:reportResults.fields.accuracyAck')}
          value={Boolean(reportResults.accuracyAcknowledged)}
          onChange={handleAckChange}
        />
      </div>

      {validationMessage && (
        <p className="nhsn-link__form-error" role="alert">
          {validationMessage}
        </p>
      )}

      <StepActions saving={saving}>
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={handleNext} disabled={saving} loading={saving}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default ReportResultsStep;
