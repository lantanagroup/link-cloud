import {FacilitySummaryResponse, TestUserProfile, UserInfoResponse, UserRoleSummaryResponse} from '../shared/models';

export class UserInfoService {
  constructor(private readonly apiBaseUrl: string = '/api') {}

  async getUserInfo(activeProfile?: TestUserProfile): Promise<UserInfoResponse> {
    const headers = new Headers();
    headers.set('Accept', 'application/json');

    if (activeProfile) {
      headers.set('jwt', JSON.stringify({
        email: activeProfile.email,
        name: activeProfile.name,
        groups: activeProfile.groups,
        facilityId: activeProfile.facilityId,
        externalUserId: activeProfile.email
      }));
    }

    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/userinfo`, {
      method: 'GET',
      headers,
      credentials: 'include'
    });

    if (!response.ok) {
      throw new Error(`Unable to load user context (${response.status}).`);
    }

    return response.json() as Promise<UserInfoResponse>;
  }

  async getUsers(activeProfile: TestUserProfile): Promise<UserRoleSummaryResponse[]> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/users`, {
      method: 'GET',
      headers: this.createHeaders(activeProfile),
      credentials: 'include'
    });

    if (response.status === 204) {
      return [];
    }

    if (!response.ok) {
      throw new Error(`Unable to load users (${response.status}).`);
    }

    return response.json() as Promise<UserRoleSummaryResponse[]>;
  }

  async updateUserAdmin(activeProfile: TestUserProfile, userId: string, isAdmin: boolean): Promise<UserRoleSummaryResponse> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/users/${userId}/admin`, {
      method: 'PUT',
      headers: this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ isAdmin })
    });

    if (!response.ok) {
      throw new Error(`Unable to update admin flag (${response.status}).`);
    }

    return response.json() as Promise<UserRoleSummaryResponse>;
  }

  async updateUserStatus(activeProfile: TestUserProfile, userId: string, isActive: boolean): Promise<UserRoleSummaryResponse> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/users/${userId}/status`, {
      method: 'PUT',
      headers: this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ isActive })
    });

    if (!response.ok) {
      throw new Error(`Unable to update user status (${response.status}).`);
    }

    return response.json() as Promise<UserRoleSummaryResponse>;
  }

  async getFacilities(activeProfile: TestUserProfile): Promise<FacilitySummaryResponse[]> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/facilities`, {
      method: 'GET',
      headers: this.createHeaders(activeProfile),
      credentials: 'include'
    });

    if (response.status === 204) {
      return [];
    }

    if (!response.ok) {
      throw new Error(`Unable to load facilities (${response.status}).`);
    }

    return response.json() as Promise<FacilitySummaryResponse[]>;
  }

  async updateFacilityOnboarding(activeProfile: TestUserProfile, facilityId: string, isOnboarded: boolean): Promise<FacilitySummaryResponse> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/facilities/${facilityId}/onboarding`, {
      method: 'PUT',
      headers: this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ isOnboarded })
    });

    if (!response.ok) {
      throw new Error(`Unable to update facility onboarding (${response.status}).`);
    }

    return response.json() as Promise<FacilitySummaryResponse>;
  }

  private createHeaders(activeProfile?: TestUserProfile): Headers {
    const headers = new Headers();
    headers.set('Accept', 'application/json');
    headers.set('Content-Type', 'application/json');

    if (activeProfile) {
      headers.set('jwt', JSON.stringify({
        email: activeProfile.email,
        name: activeProfile.name,
        groups: activeProfile.groups,
        facilityId: activeProfile.facilityId,
        externalUserId: activeProfile.email
      }));
    }

    return headers;
  }
}