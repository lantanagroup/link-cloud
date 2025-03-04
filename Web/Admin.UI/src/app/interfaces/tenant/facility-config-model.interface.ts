import { PaginationMetadata } from "../../models/pagination-metadata.model";

export interface IFacilityConfigModel {
  id?: string;
  facilityId: string;
  facilityName: string;
  timezone?: string;
  scheduledReports: IScheduledReportModel;
}

export interface IScheduledReportModel {
  daily: string[];
  weekly: string[];
  monthly: string[];
}

export interface IReportTypeScheduleModel {
  reportType: string;
  scheduledTriggers: string[];
}

export class PagedFacilityConfigModel {
  records: IFacilityConfigModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}
