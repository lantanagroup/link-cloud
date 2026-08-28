import React, {FormEvent, useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {AppRoot} from '../../core/AppRoot';
import type {ApiClient} from '../../core/api/ApiClient';
import {BffApiClient} from '../../core/api/BffApiClient';
import {MockApiClient} from '../mocks/MockApiClient';
import {TestAuthApiClient} from '../auth/TestAuthApiClient';
import {TestUserProfile} from '../auth/models';
import {
    loadActiveProfileId,
    loadProfiles,
    removeActiveProfileId,
    saveActiveProfileId,
    saveProfiles
} from '../auth/test-user-storage';
import './harness.css';

/**
 * THROWAWAY developer UI.
 *
 * Stands in for facility context until the BFF resolves it from the token.
 * Deliberately not a step and not in `flow.ts`: it has no StepId and never
 * enters the machine, so it can be deleted in one commit. Facility selection
 * is on the ADR's forbidden list, which is why it lives in `shell/` where the
 * embed bundle cannot reach it.
 */

/** Offline fixtures, or a real signed JWT against the BFF on :8079. */
type HarnessMode = 'mock' | 'bff';

type EditorState = {
  id?: string;
  label: string;
  email: string;
  name: string;
  groups: string;
  facilityId: string;
  facilityName: string;
  issuer: string;
  keyId: string;
  privateKeyPem: string;
};

function getRuntimeDefaults() {
  const runtimeConfig = window.__NHSN_APP_UI_CONFIG__;
  return {
    issuer: runtimeConfig?.defaultJwtIssuer?.trim() || 'https://dev-nhsn-app.example.org',
    keyId: runtimeConfig?.defaultJwtKeyId?.trim() || '',
    privateKeyPem: runtimeConfig?.defaultJwtPrivateKeyPem || ''
  };
}

function createEmptyEditor(): EditorState {
  const defaults = getRuntimeDefaults();
  return {
    label: '',
    email: '',
    name: '',
    groups: '',
    facilityId: '',
    facilityName: '',
    issuer: defaults.issuer,
    keyId: defaults.keyId,
    privateKeyPem: defaults.privateKeyPem
  };
}

export function FacilityHarness() {
  const {t} = useTranslation('common');
  const [profiles, setProfiles] = useState<TestUserProfile[]>(() => loadProfiles());
  const [activeId, setActiveId] = useState<string | null>(() => loadActiveProfileId());
  const [editor, setEditor] = useState<EditorState>(() => createEmptyEditor());
  const [mode, setMode] = useState<HarnessMode>('mock');
  const [collapsed, setCollapsed] = useState(true);

  const activeProfile = useMemo(() => profiles.find(profile => profile.id === activeId) ?? null, [profiles, activeId]);

  // Memoized: built inline in JSX the client is rebuilt every render and
  // re-renders every consumer.
  const client = useMemo<ApiClient | null>(() => {
    if (mode === 'mock') {
      return new MockApiClient(activeProfile?.facilityId, activeProfile?.facilityName);
    }
    if (!activeProfile) {
      return null;
    }
    return new TestAuthApiClient(new BffApiClient('/api'), activeProfile);
  }, [mode, activeProfile]);

  const sortedProfiles = useMemo(
    () => [...profiles].sort((left, right) => new Date(right.lastUsedOn).getTime() - new Date(left.lastUsedOn).getTime()),
    [profiles]
  );

  function persist(nextProfiles: TestUserProfile[], nextActiveId: string | null) {
    setProfiles(nextProfiles);
    saveProfiles(nextProfiles);
    setActiveId(nextActiveId);

    if (nextActiveId) {
      saveActiveProfileId(nextActiveId);
    } else {
      removeActiveProfileId();
    }
  }

  function activateProfile(profileId: string) {
    const now = new Date().toISOString();
    const nextProfiles = profiles.map(profile =>
      profile.id === profileId
        ? { ...profile, lastUsedOn: now }
        : profile
    );

    persist(nextProfiles, profileId);
  }

  function editProfile(profile: TestUserProfile) {
    setEditor({
      id: profile.id,
      label: profile.label,
      email: profile.email,
      name: profile.name,
      groups: profile.groups.join(', '),
      facilityId: profile.facilityId,
      facilityName: profile.facilityName,
      issuer: profile.issuer,
      keyId: profile.keyId,
      privateKeyPem: profile.privateKeyPem
    });
  }

  function clearEditor() {
    setEditor(createEmptyEditor());
  }

  function removeProfile(profileId: string) {
    const nextProfiles = profiles.filter(profile => profile.id !== profileId);
    const nextActive = activeId === profileId ? (nextProfiles[0]?.id ?? null) : activeId;
    persist(nextProfiles, nextActive);
    clearEditor();
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const nextProfile: TestUserProfile = {
      id: editor.id ?? crypto.randomUUID(),
      label: editor.label || editor.email,
      email: editor.email.trim(),
      name: editor.name.trim(),
      groups: editor.groups.split(',').map(item => item.trim()).filter(Boolean),
      facilityId: editor.facilityId.trim(),
      facilityName: editor.facilityName.trim(),
      issuer: editor.issuer.trim(),
      keyId: editor.keyId.trim(),
      privateKeyPem: editor.privateKeyPem.trim(),
      lastUsedOn: new Date().toISOString()
    };

    const nextProfiles = editor.id
      ? profiles.map(profile => (profile.id === editor.id ? nextProfile : profile))
      : [...profiles, nextProfile];

    persist(nextProfiles, nextProfile.id);
    clearEditor();
  }

  return (
    <div className={`shell${collapsed ? ' shell--collapsed' : ''}`}>
      <aside className="shell__sidebar">
        <button
          type="button"
          className="shell__toggle"
          onClick={() => setCollapsed(!collapsed)}
          aria-expanded={!collapsed}
          aria-label={collapsed ? t('shell.expandLabel') : t('shell.collapseLabel')}
          title={collapsed ? t('shell.expandLabel') : t('shell.collapseLabel')}>
          {collapsed ? '›' : '‹'}
        </button>

        <div className="shell__sidebar-content">
          <h1>{t('app.title')}</h1>
          <p className="shell__tagline">
            {t('shell.tagline')}
          </p>

          <div className="shell__card">
            <h2>{t('shell.modeTitle')}</h2>
            <div className="shell__row">
              <label htmlFor="harnessMode">{t('shell.modeLabel')}</label>
              <select
                id="harnessMode"
                value={mode}
                onChange={event => setMode(event.target.value as HarnessMode)}>
                <option value="mock">{t('shell.modeMock')}</option>
                <option value="bff">{t('shell.modeBff')}</option>
              </select>
            </div>
            <small>{mode === 'mock' ? t('shell.modeMockHint') : t('shell.modeBffHint')}</small>
          </div>

          <div className="shell__card">
            <h2>{t('shell.savedTestUsersTitle')}</h2>
            {sortedProfiles.length === 0 ? (
              <p>{t('shell.noSavedUsers')}</p>
            ) : (
              <ul className="shell__list">
                {sortedProfiles.map(profile => (
                  <li key={profile.id}>
                    <button
                      className={profile.id === activeId ? 'active' : ''}
                      type="button"
                      onClick={() => activateProfile(profile.id)}>
                      {profile.label}
                    </button>
                    <div className="shell__profile-meta">
                      <small>{t('shell.signedJwtPrefix')} ({profile.issuer}{profile.keyId ? `; kid=${profile.keyId}` : ''})</small>
                      <button type="button" onClick={() => editProfile(profile)}>{t('actions.edit')}</button>
                      <button type="button" className="shell__button--danger" onClick={() => removeProfile(profile.id)}>{t('actions.remove')}</button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="shell__card">
            <h2>{editor.id ? t('shell.updateProfileTitle') : t('shell.createProfileTitle')}</h2>
            <form onSubmit={handleSubmit}>
              <div className="shell__row">
                <label htmlFor="label">{t('shell.form.friendlyLabel')}</label>
                <input id="label" value={editor.label} onChange={event => setEditor({ ...editor, label: event.target.value })} required />
              </div>
              <div className="shell__row">
                <label htmlFor="email">{t('shell.form.email')}</label>
                <input id="email" type="email" value={editor.email} onChange={event => setEditor({ ...editor, email: event.target.value })} required />
              </div>
              <div className="shell__row">
                <label htmlFor="name">{t('shell.form.name')}</label>
                <input id="name" value={editor.name} onChange={event => setEditor({ ...editor, name: event.target.value })} required />
              </div>
              <div className="shell__row">
                <label htmlFor="groups">{t('shell.form.groups')}</label>
                <input id="groups" value={editor.groups} onChange={event => setEditor({ ...editor, groups: event.target.value })} placeholder={t('shell.form.groupsPlaceholder')} />
              </div>
              <div className="shell__row">
                <label htmlFor="facilityId">{t('shell.form.facilityId')}</label>
                <input id="facilityId" value={editor.facilityId} onChange={event => setEditor({ ...editor, facilityId: event.target.value })} required />
              </div>
              <div className="shell__row">
                <label htmlFor="facilityName">{t('shell.form.facilityName')}</label>
                <input id="facilityName" value={editor.facilityName} onChange={event => setEditor({ ...editor, facilityName: event.target.value })} />
              </div>
              <div className="shell__row">
                <label htmlFor="issuer">{t('shell.form.jwtIssuer')}</label>
                <input id="issuer" value={editor.issuer} onChange={event => setEditor({ ...editor, issuer: event.target.value })} />
              </div>
              <div className="shell__row">
                <label htmlFor="keyId">{t('shell.form.jwtKeyId')}</label>
                <input id="keyId" value={editor.keyId} onChange={event => setEditor({ ...editor, keyId: event.target.value })} placeholder={t('shell.form.jwtKeyIdPlaceholder')} />
              </div>
              <div className="shell__row">
                <label htmlFor="privateKeyPem">{t('shell.form.jwtPrivateKeyPem')}</label>
                <textarea id="privateKeyPem" value={editor.privateKeyPem} onChange={event => setEditor({ ...editor, privateKeyPem: event.target.value })} rows={8} placeholder={t('shell.form.jwtPrivateKeyPlaceholder')} />
              </div>
              <div className="shell__row">
                <button type="submit">{t('actions.saveAndActivate')}</button>
              </div>
              {editor.id && (
                <div className="shell__row">
                  <button type="button" onClick={clearEditor}>{t('actions.cancelEdit')}</button>
                </div>
              )}
            </form>
          </div>
        </div>
      </aside>

      <main className="shell__content">
        {client ? (
          <AppRoot client={client} baseUrl="/" />
        ) : (
          <div className="shell__empty">
            <h2>{t('state.selectOrCreateProfile')}</h2>
            <p>{t('shell.selectOrCreateDescription')}</p>
          </div>
        )}
      </main>
    </div>
  );
}

export default FacilityHarness;

