export interface TestUserProfile {
  id: string;
  label: string;
  email: string;
  name: string;
  groups: string[];
  facilityId: string;
  lastUsedOn: string;
}

export interface UserInfoResponse {
  Email: string;
  Name: string;
  Roles: string[];
  IsSystemAdmin: boolean;
  IsOnboarded: boolean;
  IsActive: boolean;
  FacilityId?: string;
  Groups: string[];
  AvailableNavigation: string[];
  AccessRequestUrl?: string;
}

export interface UserRoleSummaryResponse {
  Id: string;
  Email: string;
  Name: string;
  FacilityId?: string;
  IsOnboarded: boolean;
  IsActive: boolean;
  Roles: string[];
}