import {TestUserProfile} from './models';

const profilesKey = 'nhsn-app-ui.testUsers';
const activeProfileKey = 'nhsn-app-ui.activeTestUserId';
const defaultIssuer = 'https://dev-nhsn-app.example.org';

function normalizeProfile(profile: Partial<TestUserProfile> & Pick<TestUserProfile, 'id' | 'label' | 'email' | 'name' | 'groups' | 'facilityId' | 'lastUsedOn'>): TestUserProfile {
  return {
    ...profile,
    issuer: profile.issuer?.trim() || defaultIssuer,
    keyId: profile.keyId?.trim() || '',
    privateKeyPem: profile.privateKeyPem ?? ''
  };
}

export function loadProfiles(): TestUserProfile[] {
  const raw = window.localStorage.getItem(profilesKey);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as Array<Partial<TestUserProfile> & Pick<TestUserProfile, 'id' | 'label' | 'email' | 'name' | 'groups' | 'facilityId' | 'lastUsedOn'>>;
    return Array.isArray(parsed) ? parsed.map(normalizeProfile) : [];
  } catch {
    return [];
  }
}

export function saveProfiles(profiles: TestUserProfile[]): void {
  window.localStorage.setItem(profilesKey, JSON.stringify(profiles));
}

export function loadActiveProfileId(): string | null {
  return window.localStorage.getItem(activeProfileKey);
}

export function saveActiveProfileId(id: string): void {
  window.localStorage.setItem(activeProfileKey, id);
}

export function removeActiveProfileId(): void {
  window.localStorage.removeItem(activeProfileKey);
}
