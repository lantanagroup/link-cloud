export interface AcquisitionLog {
    id: string;
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
    resourcesAcquired?: string[];
    referencedResources?: ReferencedResource[];
    notes?: string[];
    scheduledReport: ScheduledReport;
}

export interface ReferencedResource {
    queryPhase: string;
    identifier: string;
}

export interface ResourceReferenceType {
    queryPhase: string;
    referenceType: string;
}

export interface FhirQuery {
    QueryType: string;
    resourceTypes: string[];
    queryParameters: string[];
    query: string;
    referenceTypes?: ResourceReferenceType[];
    paged?: number;
}

export interface ScheduledReport {
    reportId: string;
    measure: string;
    startDate: Date;
    endDate: Date;
}
