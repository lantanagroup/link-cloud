export interface IDataAcquisitionFhirListConfigModel {
    id: string;
    facilityId: string;
    fhirBaseServerUrl: string;
    ehrPatientLists: IEhrPatientListModel[];
}

export interface IEhrPatientListModel {
  status?: string;
  timeFrame?: string;
  fhirId?: string;
}
