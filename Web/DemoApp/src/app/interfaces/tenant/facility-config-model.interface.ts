import { PaginationMetadata } from "../../models/pagination-metadata.model";

export interface IFacilityConfigModel {
  id?: string;
  facilityId: string;
  facilityName: string;
  scheduledTasks: IScheduledTaskModel[];
}

export interface IScheduledTaskModel {
  kafkaTopic: string;
  reportTypeSchedules: IReportTypeScheduleModel[];
}

export interface IReportTypeScheduleModel {
  reportType: string;
  scheduledTriggers: string[];
}

export class PagedFacilityConfigModel {
  records: IFacilityConfigModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}
