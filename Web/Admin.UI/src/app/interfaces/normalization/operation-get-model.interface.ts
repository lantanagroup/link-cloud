import {PaginationMetadata} from "../../models/pagination-metadata.model";
import {IOperation} from "./operation.interface";
import {IResource} from "./resource-interface";
import { CodeMapOperation } from "src/app/interfaces/normalization/code-map-operation-interface";
import { ConditionalTransformOperation } from "src/app/interfaces/normalization/conditional-transformation-operation-interface";
import { CopyPropertyOperation } from "src/app/interfaces/normalization/copy-property-interface";

 export interface IOperationModel {
   id: string;
   facilityId: string;
   operationJson: string;
   parsedOperationJson: CopyPropertyOperation | ConditionalTransformOperation | CodeMapOperation | IOperation;
   operationType: string;
   description: string;
   isDisabled: boolean;
   createDate: string;
   modifyDate?: string;
   operationResourceTypes: IOperationResourceTypeModel[];
   vendorPresets: VendorOperationPresetModel[];
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
   records: IOperationModel[];
   metadata: PaginationMetadata;
 }

export interface IOperationResourceTypeModel {
  id: string;
  operationId: string;
  resourceTypeId: string;
  operation?: IOperationModel;
  resource?: IResource;
}
