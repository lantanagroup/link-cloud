export interface IReportSchedule {
  id: string;
  createDate: Date;
  modifyDate?: Date;
  facilityId: string;
  reportStartDate: Date;
  reportEndDate: Date;
  submitReportDateTime?: Date;
  enableSubmission: boolean;
  endOfReportPeriodJobHasRun: boolean;
  reportTypes: string[];
  adHocType?: string;
  frequency: string;
  payloadRootUri?: string;
  status: string;
}

export interface IPagedReportSchedule {
  records: IReportSchedule[];
  metadata: {
    pageSize: number;
    pageNumber: number;
    totalCount: number;
    totalPages: number;
  };
}
