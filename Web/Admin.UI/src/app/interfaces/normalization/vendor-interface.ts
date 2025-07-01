export interface IVendor {
  id: string;
  name: string;
}

export interface IVendorVersion {
  vendor: IVendor;
  version: string;
}
