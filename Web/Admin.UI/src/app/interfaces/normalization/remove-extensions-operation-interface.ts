import {IOperation} from "./operation.interface";

export interface RemoveExtensionsOperation extends IOperation {
  ExtensionUrls: string[];
}
