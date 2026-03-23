export interface IReportEntry {
  id: string;
  createDate: Date;
  modifyDate?: Date;
  facilityId: string;
  reportScheduleId: string;
  patientId: string;
  reportingStatus: ReportingStatus;
  submissionStatus?: SubmissionStatus;
  submitReportDateTime?: Date;
  aggregateReportUri: string;
  aggregateReportBlobName: string;
  measureReports: IEvaluatedMeasureReport[];
}

export interface IEvaluatedMeasureReport {
  measureReportId: string;
  status: MeasureReportStatus;
  reportType: string;
  measureReportUri: string;
  measureReportFileName: string;
  resourceCount: Record<string, number>;
}

export interface IPagedReportEntry {
  records: IReportEntry[];
  metadata: {
    pageSize: number;
    pageNumber: number;
    totalCount: number;
    totalPages: number;
  };
}

export interface IReportEntrySummary {
  reportTypeCounts: Record<string, number>;
  reportingStatusCounts: Record<string, number>;
  submissionStatusCounts: Record<string, number>;
}

export enum ReportingStatus {
  PatientIdentified,
  NotReportable,
  PendingValidation,
  PassedValidation,
  FailedValidation,
}

export enum SubmissionStatus {
  PendingValidation,
  Submitting,
  Submitted,
  FailedSubmission,
  NotEligable,
}

export enum MeasureReportStatus {
  EntryCreated,
  NotReportable,
  ReadyForValidation,
}
