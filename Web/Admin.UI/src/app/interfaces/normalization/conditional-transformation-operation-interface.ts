import {IOperation} from "./operation.interface";

export interface TransformCondition {
  FhirPathSource: string;
  Operator: string;
  Value: any;
}

export interface ConditionalTransformOperation extends IOperation{
  TargetFhirPath: string;
  TargetValue: any;
  Conditions: TransformCondition[];
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

