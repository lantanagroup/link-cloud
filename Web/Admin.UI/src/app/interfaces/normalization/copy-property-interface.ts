import {IOperation} from "./operation.interface";

export interface CopyPropertyOperation  extends IOperation {
  SourceFhirPath: string;
  TargetFhirPath: string;
}
