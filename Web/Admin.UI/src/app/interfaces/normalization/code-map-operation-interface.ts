import {IOperation} from "./operation.interface";

export interface CodeMap {
  Code: string;
  Display: string;
}

export interface CodeSystemMap {
  id?: number;
  SourceSystem: string;
  TargetSystem: string;
  CodeMaps: Record<string, CodeMap>;
}

export interface CodeMapOperation extends IOperation {
  FhirPath: string;
  CodeSystemMaps: CodeSystemMap[];
}
