export interface TestUserProfile {
  id: string;
  label: string;
  email: string;
  name: string;
  groups: string[];
  facilityId: string;
  issuer: string;
  keyId: string;
  privateKeyPem: string;
  lastUsedOn: string;
}

export interface UserInfoResponse {
  AccessState: 'Allowed' | 'MissingFacility' | 'MissingRequiredRole';
  Email: string;
  Name: string;
  IsFacilityAdmin: boolean;
  IsOnboarded: boolean;
  HasFacility: boolean;
  FacilityId?: string;
  Groups: string[];
  AvailableNavigation: string[];
  AccessRequestUrl?: string;
}

export interface FacilitySummaryResponse {
  Id: string;
  FacilityId: string;
  IsOnboarded: boolean;
}
