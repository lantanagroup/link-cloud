import {IOperationResourceTypeModel, VendorOperationPresetModel} from "./operation-get-model.interface";

export interface IOperationSequenceModel {
  id: string;
  facilityId: string;
  sequence: number;
  operationResourceType: IOperationResourceTypeModel;
  vendorPresets: VendorOperationPresetModel[];
}


