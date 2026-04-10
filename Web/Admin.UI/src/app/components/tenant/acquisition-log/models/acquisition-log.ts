export interface AcquisitionLog {
    id: string;
    reportTrackingId: string;
    priority: string;
    facilityId: string;
    patientId: string;
    fhirVersion: string;
    queryType: string;
    queryPhase: string;
    fhirQuery: FhirQuery;
    status: string;
    executionDate: Date;
    timeZone: string;
    retryAttempts: number;
    completionDate?: Date;
    completionTimeMilliseconds?: number;
    resourceAcquiredIds?: string[];
    referenceResourceCount?: number;
    notes?: string[];
    scheduledReport: ScheduledReport;
    isDeleted: boolean;
}

export interface ReferencedResource {
    facilityId: string;
    resourceId: string;
    resourceType: string;
    queryPhase: string;
}

export interface ResourceReferenceType {
    queryPhase: string;
    resourceType: string;
}

export interface FhirQuery {
    queryType: string;
    resourceTypes: string[];
    queryParameters: string[];
    query: string;
    referenceTypes?: ResourceReferenceType[];
    paged?: number;
}

export interface ScheduledReport {
    reportId: string;
    reportTypes: string[];
    startDate: Date;
    endDate: Date;
}
