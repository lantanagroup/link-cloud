import { PaginationMetadata } from "src/app/models/pagination-metadata.model";

export interface AcquisitionLogSummary {
    id: string;
    priority: string;
    patientId: string;
    facilityId: string;
    resourceTypes: string[];
    resourceId: string;
    fhirVersion: string;
    queryPhase: string;
    queryType: string;
    executionDate: Date;    
    status: string;
    reportIds: string[];
}

export interface IPagedAcquisitionLogSummary {
   records: AcquisitionLogSummary[];
   metadata: PaginationMetadata;
 }
