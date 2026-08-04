export interface IVendor {
  id: string;
  name: string;
}

export interface IVendorVersion {
  id: string;
  vendorId: string;
  vendorName: string;
  version: string;
}
