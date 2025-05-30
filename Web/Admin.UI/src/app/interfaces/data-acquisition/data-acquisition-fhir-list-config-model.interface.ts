import { IDataAcquisitionAuthenticationConfigModel } from "./data-acquisition-auth-config-model.interface";

export interface IDataAcquisitionFhirListConfigModel {
    id: string;
    facilityId: string;
    fhirBaseServerUrl: string;
    authentication?: IDataAcquisitionAuthenticationConfigModel;
    ehrPatientLists: IEhrPatientListModel[];
}

export interface IEhrPatientListModel {
    listIds: string[];
    measureIds: string[];
}
