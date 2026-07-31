import React, {FormEvent, useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {NHSNLink} from '../components/NHSNLink';
import {TestUserProfile} from '../shared/models';
import {
    loadActiveProfileId,
    loadProfiles,
    removeActiveProfileId,
    saveActiveProfileId,
    saveProfiles
} from '../shared/test-user-storage';
import './App.css';

type EditorState = {
  id?: string;
  label: string;
  email: string;
  name: string;
  groups: string;
  facilityId: string;
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
    issuer: defaults.issuer,
    keyId: defaults.keyId,
    privateKeyPem: defaults.privateKeyPem
  };
}

export function App() {
  const {t} = useTranslation('common');
  const [profiles, setProfiles] = useState<TestUserProfile[]>(() => loadProfiles());
  const [activeId, setActiveId] = useState<string | null>(() => loadActiveProfileId());
  const [editor, setEditor] = useState<EditorState>(() => createEmptyEditor());

  const activeProfile = useMemo(() => profiles.find(profile => profile.id === activeId) ?? null, [profiles, activeId]);

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
    <div className="shell">
      <aside className="shell__sidebar">
        <h1>{t('app.title')}</h1>
        <p className="shell__tagline">
          {t('shell.tagline')}
        </p>

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
                  <div style={{ marginTop: '0.35rem', display: 'grid', gap: '0.35rem' }}>
                    <small>{t('shell.signedJwtPrefix')} ({profile.issuer}{profile.keyId ? `; kid=${profile.keyId}` : ''})</small>
                    <button type="button" onClick={() => editProfile(profile)}>{t('actions.edit')}</button>
                    <button type="button" onClick={() => removeProfile(profile.id)}>{t('actions.remove')}</button>
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
      </aside>

      <main className="shell__content">
        {activeProfile ? (
          <NHSNLink activeTestUser={activeProfile} baseUrl="/" apiBaseUrl="/api" />
        ) : (
          <div style={{ padding: '2rem' }}>
            <div style={{ background: 'white', borderRadius: '8px', padding: '1.5rem', boxShadow: '0 1px 3px rgba(0, 0, 0, 0.08)' }}>
              <h2>{t('state.selectOrCreateProfile')}</h2>
              <p>
                {t('shell.selectOrCreateDescription')}
              </p>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;

