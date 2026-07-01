import {TestUserProfile} from './models';

const profilesKey = 'nhsn-app-ui.testUsers';
const activeProfileKey = 'nhsn-app-ui.activeTestUserId';

export function loadProfiles(): TestUserProfile[] {
  const raw = window.localStorage.getItem(profilesKey);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as TestUserProfile[];
    return Array.isArray(parsed) ? parsed : [];
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