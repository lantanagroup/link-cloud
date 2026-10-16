import {
  IPagedReportEntry,
  IReportEntry,
  IReportEntrySummary,
  MeasureReportStatus,
  ReportingStatus,
  SubmissionStatus,
} from '../../src/app/interfaces/report/report-entry.interface';
import {
  IDataAcquisitionLogStatistics,
} from '../../src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';

/** The measure the demo facility is enrolled in. */
const MEASURE = 'NHSNdQMAcuteCareHospitalInitialPopulation';

interface EntrySeed {
  patientId: string;
  reportingStatus: ReportingStatus;
  submissionStatus: SubmissionStatus;
  resources: Record<string, number>;
}

/** Four patients at different points in the pipeline, so the table is not four identical rows. */
const seeds: EntrySeed[] = [
  {
    patientId: 'PAT-10431',
    reportingStatus: ReportingStatus.PassedValidation,
    submissionStatus: SubmissionStatus.Submitted,
    resources: { Encounter: 1, Condition: 4, Observation: 22, MedicationRequest: 6 },
  },
  {
    patientId: 'PAT-10447',
    reportingStatus: ReportingStatus.PassedValidation,
    submissionStatus: SubmissionStatus.Submitted,
    resources: { Encounter: 1, Condition: 2, Observation: 17, Procedure: 3 },
  },
  {
    patientId: 'PAT-10512',
    reportingStatus: ReportingStatus.PendingValidation,
    submissionStatus: SubmissionStatus.PendingValidation,
    resources: { Encounter: 1, Condition: 3, Observation: 9 },
  },
  {
    patientId: 'PAT-10588',
    reportingStatus: ReportingStatus.NotReportable,
    submissionStatus: SubmissionStatus.NotEligable,
    resources: { Encounter: 1 },
  },
];

/** GET /api/entries/search — the Measure Reports tab. */
export function reportEntries(facilityId: string, reportScheduleId: string): IPagedReportEntry {
  const records: IReportEntry[] = seeds.map((seed, i) => ({
    id: `eeeeeeee-0000-0000-0000-00000000000${i + 1}`,
    createDate: new Date('2026-06-30T02:14:00Z'),
    facilityId,
    reportScheduleId,
    patientId: seed.patientId,
    reportingStatus: seed.reportingStatus,
    submissionStatus: seed.submissionStatus,
    submitReportDateTime:
      seed.submissionStatus === SubmissionStatus.Submitted ? new Date('2026-06-30T03:02:00Z') : undefined,
    aggregateReportUri: `https://link.example/reports/${reportScheduleId}`,
    aggregateReportBlobName: `${reportScheduleId}.json`,
    measureReports: [
      {
        measureReportId: `aaaaaaaa-0000-0000-0000-00000000000${i + 1}`,
        status: MeasureReportStatus.ReadyForValidation,
        reportType: MEASURE,
        measureReportUri: `https://link.example/measure-reports/${seed.patientId}`,
        measureReportFileName: `${seed.patientId}.json`,
        resourceCount: seed.resources,
      },
    ],
  }));

  return {
    records,
    metadata: { pageSize: 10, pageNumber: 1, totalCount: records.length, totalPages: 1 },
  };
}

/**
 * GET /api/entries/schedules/{id}/summary — the donut charts above the table.
 *
 * Keyed by enum name. The screen's toReportingStatus() accepts either the name or the
 * numeric value, and falls back to showing the raw key, so a wrong key here degrades to an
 * ugly label rather than an error — worth knowing before trusting these charts in a demo.
 */
export const reportEntrySummary: IReportEntrySummary = {
  reportTypeCounts: { [MEASURE]: 4 },
  reportingStatusCounts: { PassedValidation: 2, PendingValidation: 1, NotReportable: 1 },
  submissionStatusCounts: { Submitted: 2, PendingValidation: 1, NotEligable: 1 },
};

/**
 * GET /api/data-acquisition/acquisition-logs/report/{id}/statistics — the Query Analysis
 * tab, which is only fetched when that tab is selected (onTabSelected, index 1).
 *
 * Totals are consistent with the four entries above: 70 resources across 4 patients, one
 * retry, Observation the slowest resource type.
 */
export const acquisitionLogStatistics: IDataAcquisitionLogStatistics = {
  queryTypeCounts: { Initial: 4, Supplemental: 3 },
  queryPhaseCounts: { Initial: 4, Supplemental: 3 },
  requestStatusCounts: { Succeeded: 6, Retried: 1 },
  resourceTypeCounts: { Encounter: 4, Condition: 9, Observation: 48, MedicationRequest: 6, Procedure: 3 },
  resourceTypeCompletionTimeMilliseconds: {
    Encounter: 412,
    Condition: 980,
    Observation: 3140,
    MedicationRequest: 610,
    Procedure: 305,
  },
  totalLogs: 7,
  totalPatients: 4,
  totalResourcesAcquired: 70,
  totalRetryAttempts: 1,
  totalCompletionTimeMilliseconds: 5447,
  averageCompletionTimeMilliseconds: 778,
  fastestCompletionTimeMilliseconds: { resourceType: 'Procedure', completionTimeMilliseconds: 305 },
  slowestCompletionTimeMilliseconds: { resourceType: 'Observation', completionTimeMilliseconds: 3140 },
};
