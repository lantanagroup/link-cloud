export interface IDataAcquisitionRequested {
  key: string,
  patientId: string,
  reports: IScheduledReport[],
}

export interface IScheduledReport {
  reportType: string,
  startDate: Date,
  endDate: Date
}
