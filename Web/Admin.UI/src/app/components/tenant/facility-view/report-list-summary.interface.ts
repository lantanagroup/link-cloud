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