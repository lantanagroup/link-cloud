export interface IDataAcquisitionLogStatistics {
    queryTypeCounts: Record<string, number>;
    queryPhaseCounts: Record<string, number>;
    requestStatusCounts: Record<string, number>;
    resourceTypeCounts: Record<string, number>;
    resourceTypeCompletionTimeMilliseconds: Record<string, number>;
    totalLogs: number;
    totalPatients: number;
    totalResourcesAcquired: number;
    totalRetryAttempts: number;
    totalCompletionTimeMilliseconds: number;
    averageCompletionTimeMilliseconds: number;
    fastestCompletionTimeMilliseconds: IResourceTypeCompletionTime;
    slowestCompletionTimeMilliseconds: IResourceTypeCompletionTime;
}

export interface IResourceTypeCompletionTime {
    resourceType: string;
    completionTimeMilliseconds: number;
}