import { PaginationMetadata } from "src/app/models/pagination-metadata.model";

export interface IReportListSummary
{
    id: string;
    facilityId: string;
    reportStartDate: Date;
    reportEndDate: Date;
    submitted: boolean
    submitDate: Date;
    reportTypes: string[];
    frequency: string;
    censusCount: ICensusCount;
    initialPopulationCount: number;
}

export interface ICensusCount
{
    admittedPatients: number;
    dischargedPatients: number;
}

export class IPagedReportListSummary {
  records: IReportListSummary[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}

export interface IReportSummary extends IReportListSummary
{
  patientReportSummaries: IPatientReportSummary[]; 
  sharedResources: IResourceSummary[];
}

export interface IPatientReportSummary
{
  id: string;
  patientId: string;
  reportType: string;
  status: string;
  validationStatus: string;
  patientResources: IResourceSummary[];
}

export interface IResourceSummary
{
  resourceType: string;
  resourceCategory: string;
  resourceCount: number;
}
