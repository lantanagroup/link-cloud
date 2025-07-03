import {IOperation} from "./operation.interface";

export interface ISaveOperationModel {
  id?: string
  facilityId?: string;
  description?: string;
  operation: IOperation;
  isDisabled? : boolean;
  resourceTypes : string[];
  vendorIds: string[];
}


