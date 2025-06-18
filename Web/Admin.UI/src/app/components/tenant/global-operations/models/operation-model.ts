import { CodeMapOperation } from "src/app/interfaces/normalization/code-map-operation-interface";
import { ConditionalTransformOperation } from "src/app/interfaces/normalization/conditional-transformation-operation-interface";
import { CopyPropertyOperation } from "src/app/interfaces/normalization/copy-property-interface";
import { IOperation } from "src/app/interfaces/normalization/operation.interface";
import { PaginationMetadata } from "src/app/models/pagination-metadata.model";

export interface OperationModel {
  id: string;
  facilityId: string;
  operationJson: string;
  parsedOperationJson: CopyPropertyOperation | ConditionalTransformOperation | CodeMapOperation | IOperation;
  operationType: string;
  description: string;
  isDisabled: boolean;
  createDate: string;
  modifyDate?: string;
  resources: ResourceModel[];
  vendorPresets: VendorOperationPresetModel[];
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

export interface IPagedOperationModel {
  records: OperationModel[];
  metadata: PaginationMetadata;
}