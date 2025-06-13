import { PaginationMetadata } from "src/app/models/pagination-metadata.model";

export interface OperationModel {
  id: string;
  facilityId: string;
  operationJson: string;
  parsedOperationJson: IOperationJson;
  operationType: string;
  description: string;
  isDisabled: boolean;
  createDate: string;
  modifyDate?: string;
  resources: ResourceModel[];
  vendorPresets: VendorOperationPresetModel[];
}

export interface IOperationJson {
  operationType: number;
  name: string;
  description: string;
  sourceFhirPath: string;
  targetFhirPath: string;
}

export interface ResourceModel {
  resourceTypeId: string;
  resourceName: string;
}

export interface VendorOperationPresetModel {
  id: string;
  vendor?: string;
  versions?: string;
  description?: string;
  createDate: string; 
  modifyDate?: string;
}

export class IPagedOperationModel {
  records: OperationModel[] = [];
  metadata: PaginationMetadata = new PaginationMetadata;
}