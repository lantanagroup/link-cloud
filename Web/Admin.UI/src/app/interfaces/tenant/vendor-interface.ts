export interface IVendorAuthentication {
  signingKeySecretId?: string | null;
}

export interface IVendor {
  id: string;
  name: string;
  authentication?: IVendorAuthentication | null;
}

export interface IVendorVersion {
  id: string;
  vendorId: string;
  vendorName: string;
  version: string;
}
