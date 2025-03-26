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
  resourceCountSummary: IResourceCountSummary[];
}

export interface IResourceCountSummary
{
  resourceType: string;
  resourceCount: number;
}

export class IPagedMeasureReportSummary
{
  records: IMeasureReportSummary[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}

export interface IResourceSummary
{
  facilityId: string;
  measureReportId: string;
  patientId: string;
  resourceId: string;
  fhirId: string;
  resourceType: string;
  resourceCategory: string;
  reference: string;
}

export class IPagedResourceSummary
{
  records: IResourceSummary[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}