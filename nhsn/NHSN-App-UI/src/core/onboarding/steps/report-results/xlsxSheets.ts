import type {
  AcquisitionLogEntry,
  ReportDetail,
  ReportPatientEntry,
} from '../../../api/contracts';
import { formatDate, formatDateTime } from './format';
import type { TFn } from './patientRows';
import type { ParsedQueryPlan, ParsedQueryPlanQuery } from './queryPlan';
import { dqmIdForMeasureName, dqmLabel, friendlyMeasuresFor } from './reportMeasures';
import type { XlsxSheet } from './reportExport';
import { toStatusCategory } from './reportStatus';

// ---------------------------------------------------------------- report summary export
//
// Builds the "Export Report Summary" workbook: one .xlsx with a sheet per section of the
// onboarding POC's zip-of-files export (Report Summary, Selected Measures, Patient Reporting
// Status, Acquisition Log, Query Plan), rather than a zip of separate files -- see reportExport.ts.
// Each sheet covers every measure/patient/query, not just whichever DQM tab is active on screen.

export function buildReportSummarySheet(detail: ReportDetail): XlsxSheet {
  const rows: Array<[string, string]> = [['Report Id', detail.reportId]];
  if (detail.regeneratedFrom) {
    rows.push(['Regenerated From', detail.regeneratedFrom]);
  }
  rows.push(
    [
      'Reporting Period',
      `${formatDate(detail.startDate)} to ${formatDate(detail.endDate)}`,
    ],
    ['Create Date', formatDateTime(detail.createDate)],
    ['Patient Count', String(detail.patientCount)],
    ['Status', detail.status],
  );
  return { name: 'Report Summary', headers: ['Field', 'Value'], rows };
}

export function buildSelectedMeasuresSheet(
  detail: ReportDetail,
  requestedMeasuresByReportId: Record<string, string[]> | undefined,
): XlsxSheet {
  const measures = friendlyMeasuresFor(
    detail.measures,
    detail.reportId,
    detail.measureMapping,
    requestedMeasuresByReportId,
  );
  const rows = measures.map((measure) => [
    measure,
    dqmIdForMeasureName(measure, detail) ?? '',
  ]);
  return {
    name: 'Selected Measures',
    headers: ['NHSN Measure', 'Digital Quality Measure'],
    rows,
  };
}

export function buildPatientReportingStatusSheet(
  patients: ReportPatientEntry[],
  measureMapping: ReportDetail['measureMapping'],
  t: TFn,
): XlsxSheet {
  const rows = patients.flatMap((patient) => {
    const reports =
      patient.measureReports.length > 0 ? patient.measureReports : [null];
    return reports.map((measureReport) => [
      patient.patientId,
      measureReport ? dqmLabel(measureReport.reportType, measureMapping) : '—',
      String(measureReport?.resourceCount ?? patient.resourceCount),
      t(
        `onboarding:reportResults.detail.statusCategories.${toStatusCategory(patient.reportingStatus)}`,
      ),
      patient.locationOrgMapped
        ? t('onboarding:reportResults.detail.mapping.found')
        : t('onboarding:reportResults.detail.mapping.notFound'),
      patient.hslocMapped
        ? t('onboarding:reportResults.detail.mapping.found')
        : t('onboarding:reportResults.detail.mapping.notFound'),
      patient.encounterMapped
        ? t('onboarding:reportResults.detail.mapping.found')
        : t('onboarding:reportResults.detail.mapping.notFound'),
    ]);
  });
  return {
    name: 'Patient Reporting Status',
    headers: [
      'Patient Id',
      'Measure',
      'FHIR Resource Count',
      'Report Status',
      'Location Org Found',
      'HSLOC Mapping Found',
      'Encounter Mapping Found',
    ],
    rows,
  };
}

export function buildAcquisitionLogSheet(entries: AcquisitionLogEntry[]): XlsxSheet {
  return {
    name: 'Acquisition Log',
    headers: [
      'Patient Id',
      'Resource',
      'Query Phase',
      'Query Type',
      'Parameters',
      'Status',
    ],
    rows: entries.map((entry) => [
      entry.patientId,
      entry.resource,
      entry.queryPhase,
      entry.queryType ?? '',
      entry.parameters.join('; '),
      entry.status,
    ]),
  };
}

function queryPlanRows(plan: ParsedQueryPlan): Array<Array<string>> {
  const row = (section: string, query: ParsedQueryPlanQuery) => [
    section,
    query.resourceType,
    query.queryConfigType ?? '',
    query.operationType ?? '',
    query.paged === undefined ? '' : query.paged ? 'Yes' : 'No',
    query.parameters.join('; '),
  ];
  return [
    ...plan.initialQueries.map((query) => row('Initial', query)),
    ...plan.supplementalQueries.map((query) => row('Supplemental', query)),
  ];
}

export function buildQueryPlanSheet(plan: ParsedQueryPlan): XlsxSheet {
  return {
    name: 'Query Plan',
    headers: [
      'Section',
      'Resource Type',
      'Query Type',
      'Operation Type',
      'Paged',
      'Parameters',
    ],
    rows: queryPlanRows(plan),
  };
}
