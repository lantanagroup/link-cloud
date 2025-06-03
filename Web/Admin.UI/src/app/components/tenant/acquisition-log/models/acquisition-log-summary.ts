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
    scheduledDate: Date;    
    status: string;
    reportIds: string[];
}
