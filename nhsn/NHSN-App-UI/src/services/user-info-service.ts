import {TestUserProfile, UserInfoResponse, UserRoleSummaryResponse} from '../shared/models';

export class UserInfoService {
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

    const response = await fetch('/api/nhsn-app-bff/userinfo', {
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
    const response = await fetch('/api/nhsn-app-bff/users', {
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

  async updateUserRoles(activeProfile: TestUserProfile, userId: string, roles: string[]): Promise<UserRoleSummaryResponse> {
    const response = await fetch(`/api/nhsn-app-bff/users/${userId}/roles`, {
      method: 'PUT',
      headers: this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ roles })
    });

    if (!response.ok) {
      throw new Error(`Unable to update user roles (${response.status}).`);
    }

    return response.json() as Promise<UserRoleSummaryResponse>;
  }

  async updateUserStatus(activeProfile: TestUserProfile, userId: string, isActive: boolean): Promise<UserRoleSummaryResponse> {
    const response = await fetch(`/api/nhsn-app-bff/users/${userId}/status`, {
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