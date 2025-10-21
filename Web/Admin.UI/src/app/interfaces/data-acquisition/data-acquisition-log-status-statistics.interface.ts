export interface IDataAcquisitionLogStatusStatistics {
  reportId: string;
  patientId?: string;
  statuses: IDataAcquisitionLogStatusCount[];
}

export interface IDataAcquisitionLogStatusCount {
  name: string;
  count: number;
}
