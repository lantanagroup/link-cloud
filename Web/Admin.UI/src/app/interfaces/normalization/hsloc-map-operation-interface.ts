import {CodeMapOperation} from './code-map-operation-interface';
import {OperationType} from './operation-type-enumeration';

export interface HSLOCMapOperation extends CodeMapOperation {
  OperationType: OperationType.HSLOCMap;
}