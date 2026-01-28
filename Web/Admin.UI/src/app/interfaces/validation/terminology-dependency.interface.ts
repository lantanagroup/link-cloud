export interface ITerminologyDependency {
  url: string;
  version: string | null;
  resourceExists: boolean;
  versionExists: boolean;
}
