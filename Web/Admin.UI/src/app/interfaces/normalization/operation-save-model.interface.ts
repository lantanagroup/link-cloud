export interface ISaveOperationModel {
  Id?: string
  FacilityId?: string;
  Operation: IOperation;
  Description: string;
  IsDisabled? : boolean;
  ResourceTypes : string[];
}

export interface IOperation {
  OperationType: string;
  Name: string;
}

export enum OperationType {
  None = 0,
  CopyProperty = "CopyProperty",
  ConditionalTransform = "ConditionalTransform",
  CodeMap = "CodeMap"
}
