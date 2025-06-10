export interface IOperationModel {
  Id: string
  FacilityId?: string;
  OperationJson: string;
  OperationType: string;
  Description: string;
  IsDisabled: boolean;
  ResourceTypes?: string[];
  Resources: IResource[];
  VendorPresets?: string[];
}

export interface IResource {
  ResourceTypeId: string;
  ResourceName: string;
}
