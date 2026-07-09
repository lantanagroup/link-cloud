import {IOperation} from "./operation.interface";

export interface CopyLocationAliasToTypeIterativelyOperation extends IOperation {
  MaxIterations: number;
  SplitOnComma: boolean;
}
