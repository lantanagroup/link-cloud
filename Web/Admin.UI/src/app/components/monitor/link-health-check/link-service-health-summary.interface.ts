export interface ILinkServiceHealthSummary {
    service: string;
    status: string;
    totalDuration: string;
    entries: Record<string, ILinkServiceHealthReportEntry>;
}

export interface ILinkServiceHealthReportEntry {
    status: string;
    duration: string;
    description: string | null;
}
