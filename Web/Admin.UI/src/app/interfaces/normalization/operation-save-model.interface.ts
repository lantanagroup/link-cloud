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

export enum Operator {
  Equal = 0,
  GreaterThan = 1,
  GreaterThanOrEqual = 2,
  LessThan = 3,
  LessThanOrEqual = 4,
  NotEqual = 5,
  Exists = 6,
  NotExists = 7
}

