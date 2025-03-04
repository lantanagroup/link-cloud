export interface IReportScheduled {
  facilityId: string,
  frequency: string
  reportTypes: string[],
  startDate: Date,
  delay: string,
  endDate?: Date
}
