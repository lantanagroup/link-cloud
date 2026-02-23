export interface IPackageDetailsModel {
  version: string;
  id: string;
  name: string;
  resources: IPackageResource[];
}

export interface IPackageResource {
  resourceType: string;
  id: string;
  url: string;
  name: string;
  version: string;
}
