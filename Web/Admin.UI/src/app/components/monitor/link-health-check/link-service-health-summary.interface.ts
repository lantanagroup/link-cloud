export interface ILinkServiceHealthSummary {
    service: string;
    status: string;
    kafkaConnection: string;
    databaseConnection: string;
    cacheConnection: string;
}
