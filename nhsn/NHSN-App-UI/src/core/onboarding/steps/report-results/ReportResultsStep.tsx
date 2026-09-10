import React, {useCallback, useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {ReportDetail, ReportingStatus, ReportPatientEntry, ReportStatus, ReportSummary, PatientMappingEvidence, QueryPlan, AcquisitionLogEntry} from '../../../api/contracts';
import {PatientStatusTimelineModal} from './PatientStatusTimeline';
import {
  Button,
  CheckboxField,
  MessageContainer,
  Modal,
  NHSNLoadingIndicator,
  PageHeader,
  StepActions,
  Tabs
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {PLACEHOLDER_MEASURES} from '../report/placeholderMeasures';

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
 * The NHSN measure names Report Results/Details show for a report, resolved back from the dQMs
 * Report actually stores. Prefers the exact selection made when the report was requested (kept in
 * the draft, since Report only knows the resolved dQM); falls back to every placeholder measure
 * that resolves to one of the report's dQMs when that record isn't available -- e.g. a report
 * generated in another session -- which is ambiguous when several placeholders share a dQM but
 * still more useful than the raw dQM id.
 */
function friendlyMeasuresFor(
  dqmMeasures: string[],
  reportId: string,
  requestedMeasuresByReportId?: Record<string, string[]>
): string[] {
  const requested = requestedMeasuresByReportId?.[reportId];
  if (requested && requested.length > 0) {
    const names = requested
      .map(id => PLACEHOLDER_MEASURES.find(measure => measure.id === id)?.name)
      .filter((name): name is string => Boolean(name));
    if (names.length > 0) {
      return names;
    }
  }

  const dqmSet = new Set(dqmMeasures);
  const byDqm = PLACEHOLDER_MEASURES.filter(measure => dqmSet.has(measure.digitalQualityMeasure)).map(measure => measure.name);
  return byDqm.length > 0 ? byDqm : dqmMeasures;
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

const DQM_LINK_BY_NAME: Record<string, string> = {
  'ACH Monthly': 'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalMonthlyInitialPopulation.html',
  'ACH Daily': 'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalDailyInitialPopulation.html',
  'LTC Monthly': 'https://measures-ci.nhsnlink.org/Measure-NHSNLongTermCareMonthlyInitialPopulation.html'
};

// A DQM tab ("ACH Monthly") to the real report type Report stores measure reports under
// ("NHSNAcuteCareHospitalMonthlyInitialPopulation"). Derived from the placeholder table rather
// than hand-duplicated, since every placeholder already carries both.
const REAL_REPORT_TYPE_BY_DQM_NAME: Record<string, string> = Object.fromEntries(
  PLACEHOLDER_MEASURES.map(measure => [DIGITAL_QUALITY_MEASURE_BY_MEASURE[measure.name], measure.digitalQualityMeasure])
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

/**
 * The Report Details modal covers the report's own fields and the selected
 * measures' digital quality measure links. The patient pipeline, query plan
 * and acquisition logs seen in the POC are not built here - LEGLINK story:
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
  const {draft, patch, saving, goTo, openView, closeView} = useOnboarding();
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
  const [mappingEvidenceColumn, setMappingEvidenceColumn] = useState<'locationOrg' | 'hsloc' | 'encounter' | null>(null);
  const [mappingEvidencePatientId, setMappingEvidencePatientId] = useState<string | null>(null);
  const [mappingEvidence, setMappingEvidence] = useState<PatientMappingEvidence | null>(null);
  const [mappingEvidenceLoading, setMappingEvidenceLoading] = useState(false);
  const [mappingEvidenceError, setMappingEvidenceError] = useState<string | null>(null);

  const [queryPlanOpen, setQueryPlanOpen] = useState(false);
  const [queryPlan, setQueryPlan] = useState<QueryPlan | null>(null);
  const [queryPlanLoading, setQueryPlanLoading] = useState(false);
  const [queryPlanError, setQueryPlanError] = useState<string | null>(null);

  const [acquisitionLogOpen, setAcquisitionLogOpen] = useState(false);
  const [acquisitionLogs, setAcquisitionLogs] = useState<AcquisitionLogEntry[]>([]);
  const [acquisitionLogLoading, setAcquisitionLogLoading] = useState(false);
  const [acquisitionLogError, setAcquisitionLogError] = useState<string | null>(null);

  const [exporting, setExporting] = useState(false);

  async function handleViewQueryPlan() {
    if (!detail) {
      return;
    }
    setQueryPlanOpen(true);
    setQueryPlanLoading(true);
    setQueryPlanError(null);
    try {
      setQueryPlan(await api.getQueryPlan(detail.reportId));
    } catch (cause) {
      setQueryPlanError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setQueryPlanLoading(false);
    }
  }

  async function handleViewAcquisitionLog() {
    if (!detail) {
      return;
    }
    setAcquisitionLogOpen(true);
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

  async function handleExportSummary() {
    if (!detail) {
      return;
    }
    setExporting(true);
    try {
      const blob = await api.exportReportSummary(detail.reportId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `report-${detail.reportId}-summary.json`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setExporting(false);
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
    try {
      const evidence = await api.getPatientMappingEvidence(detail.reportId, patientId);
      setMappingEvidence(evidence);
    } catch (cause) {
      setMappingEvidenceError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setMappingEvidenceLoading(false);
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
                    const dqm = DIGITAL_QUALITY_MEASURE_BY_MEASURE[measure];
                    const dqmUrl = dqm ? DQM_LINK_BY_NAME[dqm] : undefined;
                    return (
                      <tr key={measure}>
                        <td>{measure}</td>
                        <td>
                          {dqm && dqmUrl ? (
                            <a
                              className="nhsn-link__report-results-link"
                              href={dqmUrl}
                              target="_blank"
                              rel="noopener noreferrer">
                              {dqm}
                            </a>
                          ) : (
                            dqm ?? '—'
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
                            {row.hasPreQualResults ? <ChartIcon /> : t('onboarding:reportResults.detail.notApplicable')}
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
                              disabled
                              aria-label={t('onboarding:reportResults.detail.downloadUnavailable')}
                              title={t('onboarding:reportResults.detail.downloadUnavailable')}>
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
            <Button variant="secondary" onClick={() => setQueryPlanOpen(false)}>
              {t('common:actions.close')}
            </Button>
          }>
          {queryPlanLoading && <NHSNLoadingIndicator />}
          {!queryPlanLoading && queryPlanError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{queryPlanError}</span>
            </MessageContainer>
          )}
          {!queryPlanLoading && !queryPlanError && (
            <pre className="nhsn-link__report-results-json">{queryPlan?.planJson ?? '{}'}</pre>
          )}
        </Modal>

        <Modal
          open={acquisitionLogOpen}
          title={t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}
          onClose={() => setAcquisitionLogOpen(false)}
          size="large"
          footer={
            <Button variant="secondary" onClick={() => setAcquisitionLogOpen(false)}>
              {t('common:actions.close')}
            </Button>
          }>
          {acquisitionLogLoading && <NHSNLoadingIndicator />}
          {!acquisitionLogLoading && acquisitionLogError && (
            <MessageContainer type="error" showIcon>
              <span role="alert">{acquisitionLogError}</span>
            </MessageContainer>
          )}
          {!acquisitionLogLoading && !acquisitionLogError && (
            acquisitionLogs.length > 0 ? (
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <thead>
                    <tr>
                      <th>{t('onboarding:reportResults.detail.acquisitionLog.timestamp')}</th>
                      <th>{t('onboarding:reportResults.detail.acquisitionLog.level')}</th>
                      <th>{t('onboarding:reportResults.detail.acquisitionLog.message')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {acquisitionLogs.map((entry, index) => (
                      <tr key={`${entry.timestamp}-${index}`}>
                        <td className="nhsn-link__report-results-nowrap">{formatDateTime(entry.timestamp)}</td>
                        <td>{entry.level}</td>
                        <td>{entry.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
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

        <Modal
          open={Boolean(mappingEvidenceColumn)}
          title={t(`onboarding:reportResults.detail.columns.${
            mappingEvidenceColumn === 'locationOrg' ? 'locationOrg' : mappingEvidenceColumn === 'hsloc' ? 'hslocMapping' : 'encounterMapping'
          }`)}
          onClose={() => setMappingEvidenceColumn(null)}
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

          {!mappingEvidenceLoading && !mappingEvidenceError && mappingEvidence && mappingEvidenceColumn === 'locationOrg' && (
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

          {!mappingEvidenceLoading && !mappingEvidenceError && mappingEvidence && mappingEvidenceColumn !== 'locationOrg' && (
            mappingEvidence.codeMaps.length > 0 ? (
              <div className="nhsn-link__report-results-table-scroll">
                <table className="nhsn-link__report-results-table">
                  <thead>
                    <tr>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.sourceSystem')}</th>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.targetSystem')}</th>
                      <th>{t('onboarding:reportResults.detail.mappingEvidence.unmappedCodes')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {mappingEvidence.codeMaps.map((codeMap, index) => (
                      <tr key={`${codeMap.sourceSystem}-${codeMap.targetSystem}-${index}`}>
                        <td>{codeMap.sourceSystem}</td>
                        <td>{codeMap.targetSystem}</td>
                        <td>{codeMap.unmappedCodes.length > 0 ? codeMap.unmappedCodes.join(', ') : '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p>{t('onboarding:reportResults.detail.mappingEvidence.noEvidence')}</p>
            )
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
