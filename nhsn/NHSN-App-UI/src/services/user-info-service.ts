import {importPKCS8, SignJWT} from 'jose';
import {FacilitySummaryResponse, TestUserProfile, UserInfoResponse, UserRoleSummaryResponse} from '../shared/models';

export class UserInfoService {
  constructor(private readonly apiBaseUrl: string = '/api') {}

  async getUserInfo(activeProfile?: TestUserProfile): Promise<UserInfoResponse> {
    const headers = await this.createHeaders(activeProfile);

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
      headers: await this.createHeaders(activeProfile),
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
      headers: await this.createHeaders(activeProfile),
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
      headers: await this.createHeaders(activeProfile),
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
      headers: await this.createHeaders(activeProfile),
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
      headers: await this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ isOnboarded })
    });

    if (!response.ok) {
      throw new Error(`Unable to update facility onboarding (${response.status}).`);
    }

    return response.json() as Promise<FacilitySummaryResponse>;
  }

  private async createHeaders(activeProfile?: TestUserProfile): Promise<Headers> {
    const headers = new Headers();
    headers.set('Accept', 'application/json');
    headers.set('Content-Type', 'application/json');

    if (activeProfile) {
      const signedJwt = await this.createSignedJwt(activeProfile);
      headers.set('Authorization', `Bearer ${signedJwt}`);
    }

    return headers;
  }

  private async createSignedJwt(activeProfile: TestUserProfile): Promise<string> {
    if (!activeProfile.privateKeyPem.trim()) {
      throw new Error('A private key PEM is required for the harness to sign test JWTs.');
    }

    if (!activeProfile.issuer.trim()) {
      throw new Error('An issuer is required for the harness to sign test JWTs.');
    }

    const privateKey = await importPKCS8(activeProfile.privateKeyPem, 'ES256');
    const protectedHeader: { alg: 'ES256'; typ: 'JWT'; kid?: string } = {
      alg: 'ES256',
      typ: 'JWT'
    };

    if (activeProfile.keyId.trim()) {
      protectedHeader.kid = activeProfile.keyId.trim();
    }

    return await new SignJWT({
      upn: activeProfile.email,
      userId: activeProfile.email,
      userName: activeProfile.name,
      userLoggedInAs: activeProfile.name,
      groups: activeProfile.groups,
      facility: activeProfile.facilityId
    })
      .setProtectedHeader(protectedHeader)
      .setIssuer(activeProfile.issuer)
      .setSubject(activeProfile.email)
      .setAudience('nhsn-app-bff')
      .setIssuedAt()
      .setExpirationTime('15m')
      .sign(privateKey);
  }
}
