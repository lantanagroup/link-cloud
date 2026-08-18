import { PaginationMetadata } from "../../models/pagination-metadata.model";
import { IVendor } from "./vendor-interface";

export interface IFacilityConfigModel {
  id?: string;
  facilityId: string;
  facilityName: string;
  timeZone: string;
  vendor?: IVendor;
  vendorVersionId?: string;
  scheduledReports: IScheduledReportModel;
  isDeleted?: boolean;
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
