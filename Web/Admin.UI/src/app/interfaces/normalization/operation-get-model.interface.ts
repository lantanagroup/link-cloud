import {PaginationMetadata} from "../../models/pagination-metadata.model";
import {IFacilityConfigModel} from "../tenant/facility-config-model.interface";

export interface IOperationModel {
  id: string
  facilityId?: string;
  operationJson: string;
  operationType: string;
  description: string;
  isDisabled: boolean;
  resourceTypes?: string[];
  resources: IResource[];
  vendorPresets?: string[];
}

export class PagedConfigModel {
  records: IOperationModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
}

export interface IResource {
  resourceTypeId: string;
  resourceName: string;
}
