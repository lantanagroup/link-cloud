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
  IsOnboarded: boolean;
  FacilityId?: string;
  Groups: string[];
  AvailableNavigation: string[];
}