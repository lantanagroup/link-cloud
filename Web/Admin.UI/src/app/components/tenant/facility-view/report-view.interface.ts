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

export interface IMeasureReportSummary
{
  id: string;
  patientId: string;
  reportType: string;
  status: string;
  validationStatus: string;
  resourceCount: number;
}

export class IPagedMeasureReportSummary
{
  records: IMeasureReportSummary[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}

export interface IResourceSummary
{
  resourceType: string;
  resourceCategory: string;
  resourceCount: number;
}
