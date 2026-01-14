import { PaginationMetadata } from "../../models/pagination-metadata.model";

export interface IFacilityConfigModel {
  id?: string;
  facilityId: string;
  facilityName: string;
  timeZone: string;
  scheduledReports: IScheduledReportModel;
}

export interface IScheduledReportModel {
  daily: string[];
  monthly: string[];
  weekly: string[];
}

export class PagedFacilityConfigModel {
  records: IFacilityConfigModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}

export interface IAdHocReportRequest {
  bypassSubmission : boolean;
  startDate : Date;
  endDate : Date;
  reportTypes: string[];
  patientIds: string[];
}
