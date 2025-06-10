import {PaginationMetadata} from "../../models/pagination-metadata.model";
import {IFacilityConfigModel} from "../tenant/facility-config-model.interface";

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

export class PagedConfigModel {
  Records: IOperationModel[] = [];
  PaginationMetadata: PaginationMetadata = new PaginationMetadata;
}

export interface IResource {
  ResourceTypeId: string;
  ResourceName: string;
}
