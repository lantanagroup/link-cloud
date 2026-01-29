export interface IRelatedArtifact {
  name: string;
  version?: string;
  url: string;
  libraries?: { [key: string]: string };
  found: boolean;
}
