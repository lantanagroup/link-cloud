import {beforeEach, describe, expect, it} from 'vitest';
import {loadProfiles} from './test-user-storage';

describe('test user storage', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('does not invent JWT defaults when loading an older profile', () => {
    window.localStorage.setItem('nhsn-app-ui.testUsers', JSON.stringify([{
      id: 'profile-1',
      label: 'Test profile',
      email: 'test@example.org',
      name: 'Test User',
      groups: ['FACADMIN'],
      facilityId: 'facility-1',
      lastUsedOn: '2026-09-16T00:00:00.000Z'
    }]));

    expect(loadProfiles()).toEqual([expect.objectContaining({
      issuer: '',
      keyId: '',
      privateKeyPem: ''
    })]);
  });
});