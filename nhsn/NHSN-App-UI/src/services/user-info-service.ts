import {importPKCS8, SignJWT} from 'jose';
import {FacilitySummaryResponse, TestUserProfile, UserInfoResponse} from '../shared/models';

export class UserInfoService {
  constructor(private readonly apiBaseUrl: string = '/api') {}

  async getUserInfo(activeProfile?: TestUserProfile): Promise<UserInfoResponse> {
    const headers = await this.createHeaders(activeProfile);

    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/userinfo`, {
      method: 'GET',
      headers,
      credentials: 'include'
    });

    this.handleExpiredTokenRedirect(response);

    if (!response.ok) {
      throw new Error(`Unable to load user context (${response.status}).`);
    }

    return response.json() as Promise<UserInfoResponse>;
  }

  async updateFacilityOnboarding(activeProfile: TestUserProfile, facilityId: string, isOnboarded: boolean): Promise<FacilitySummaryResponse> {
    const response = await fetch(`${this.apiBaseUrl}/nhsn-app-bff/facilities/${facilityId}/onboarding`, {
      method: 'PUT',
      headers: await this.createHeaders(activeProfile),
      credentials: 'include',
      body: JSON.stringify({ isOnboarded })
    });

    this.handleExpiredTokenRedirect(response);

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
    const normalizedPrivateKeyPem = normalizePem(activeProfile.privateKeyPem);

    if (!normalizedPrivateKeyPem) {
      throw new Error('A private key PEM is required for the harness to sign test JWTs.');
    }

    if (!activeProfile.issuer.trim()) {
      throw new Error('An issuer is required for the harness to sign test JWTs.');
    }

    let privateKey;

    try {
      privateKey = await importPKCS8(normalizedPrivateKeyPem, 'ES256');
    } catch (error) {
      const detail = error instanceof Error && error.message
        ? ` ${error.message}`
        : '';

      throw new Error(
        'The JWT private key PEM could not be parsed for ES256 signing. ' +
        'Make sure the key is a valid PKCS#8 private key, includes BEGIN/END PRIVATE KEY lines, and uses real line breaks instead of escaped \\n sequences.' +
        detail
      );
    }

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

  private handleExpiredTokenRedirect(response: Response): void {
    if (response.status !== 401) {
      return;
    }

    const redirectUrl = response.headers.get('X-Expired-Token-Redirect-Url') ?? response.headers.get('Location');
    if (!redirectUrl) {
      return;
    }

    window.location.assign(redirectUrl);
    throw new Error('JWT expired or requires refresh. Redirecting for token renewal.');
  }
}

function normalizePem(value: string): string {
  const normalized = value
    .replace(/\r\n/g, '\n')
    .replace(/\\n/g, '\n')
    .trim();

  const pkcs8Match = normalized.match(/-----BEGIN PRIVATE KEY-----([A-Za-z0-9+/=\s]+)-----END PRIVATE KEY-----/s);
  if (!pkcs8Match) {
    return normalized;
  }

  const body = pkcs8Match[1].replace(/\s+/g, '');
  const wrappedBody = body.match(/.{1,64}/g)?.join('\n') ?? body;

  return [
    '-----BEGIN PRIVATE KEY-----',
    wrappedBody,
    '-----END PRIVATE KEY-----'
  ].join('\n');
}
