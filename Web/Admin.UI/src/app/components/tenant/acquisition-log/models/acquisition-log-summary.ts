import {PaginationMetadata} from "src/app/models/pagination-metadata.model";

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
    createDate: Date;
    completionDate?: Date;
    retryAttempts: number;
    status: string;
    reportTrackingId?: string | null;
    reportIds: string[];
    isDeleted: boolean;
    reportTrackingId: string;
}

export interface IPagedAcquisitionLogSummary {
   records: AcquisitionLogSummary[];
   metadata: PaginationMetadata;
 }
