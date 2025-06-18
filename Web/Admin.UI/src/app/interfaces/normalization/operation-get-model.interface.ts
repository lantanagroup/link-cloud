import {PaginationMetadata} from "../../models/pagination-metadata.model";

export interface IOperationModel {
  id: string;
  facilityId?: string;
  operationJson: string;
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

export interface IResource {
  resourceTypeId: string;
  resourceName: string;
}
