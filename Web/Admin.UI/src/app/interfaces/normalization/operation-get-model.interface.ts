import {PaginationMetadata} from "../../models/pagination-metadata.model";
import {IOperation} from "./operation.interface";
import {IResource} from "./resource-interface";

export interface IOperationModel {
  id: string;
  facilityId?: string;
  operationJson: IOperation;
  operationType: string;
  description: string;
  isDisabled: boolean;
  resources: IResource[];       // ✅ from DB
  vendorPresets?: string[];
}

export interface IOperationViewModel extends IOperationModel {
  resourceTypes: string[];      // ✅ derived from resources
  showJson: boolean;            // ✅ UI flag
}

export class PagedConfigModel {
  records: IOperationModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
}


