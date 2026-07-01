import {TestUserProfile, UserInfoResponse} from '../shared/models';

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
}