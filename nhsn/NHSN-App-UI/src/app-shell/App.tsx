import React, {FormEvent, useMemo, useState} from 'react';
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
};

function createEmptyEditor(): EditorState {
  return {
    label: '',
    email: '',
    name: '',
    groups: '',
    facilityId: ''
  };
}

export function App() {
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
      facilityId: profile.facilityId
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
        <h1>NHSN App UI Shell</h1>
        <p className="shell__tagline">
          Lower-environment shell for switching among saved simulated user contexts while exercising the shared NHSNLink component.
        </p>

        <div className="shell__card">
          <h2>Saved test users</h2>
          {sortedProfiles.length === 0 ? (
            <p>No test users saved yet. Start by answering “who are you simulating?” below.</p>
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
                    <button type="button" onClick={() => editProfile(profile)}>Edit</button>
                    <button type="button" onClick={() => removeProfile(profile.id)}>Remove</button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="shell__card">
          <h2>{editor.id ? 'Update simulated user' : 'Who are you simulating?'}</h2>
          <form onSubmit={handleSubmit}>
            <div className="shell__row">
              <label htmlFor="label">Friendly label</label>
              <input id="label" value={editor.label} onChange={event => setEditor({ ...editor, label: event.target.value })} required />
            </div>
            <div className="shell__row">
              <label htmlFor="email">Email</label>
              <input id="email" type="email" value={editor.email} onChange={event => setEditor({ ...editor, email: event.target.value })} required />
            </div>
            <div className="shell__row">
              <label htmlFor="name">Name</label>
              <input id="name" value={editor.name} onChange={event => setEditor({ ...editor, name: event.target.value })} required />
            </div>
            <div className="shell__row">
              <label htmlFor="groups">Groups (comma separated)</label>
              <input id="groups" value={editor.groups} onChange={event => setEditor({ ...editor, groups: event.target.value })} placeholder="GRPADMIN, AUTHENTICATED" />
            </div>
            <div className="shell__row">
              <label htmlFor="facilityId">Facility ID</label>
              <input id="facilityId" value={editor.facilityId} onChange={event => setEditor({ ...editor, facilityId: event.target.value })} required />
            </div>
            <div className="shell__row">
              <button type="submit">{editor.id ? 'Save and activate user' : 'Save and activate user'}</button>
            </div>
            {editor.id && (
              <div className="shell__row">
                <button type="button" onClick={clearEditor}>Cancel edit</button>
              </div>
            )}
          </form>
        </div>
      </aside>

      <main className="shell__content">
        {activeProfile ? (
          <NHSNLink activeTestUser={activeProfile} baseUrl="/" />
        ) : (
          <div style={{ padding: '2rem' }}>
            <div style={{ background: 'white', borderRadius: '8px', padding: '1.5rem', boxShadow: '0 1px 3px rgba(0, 0, 0, 0.08)' }}>
              <h2>Select or create a simulated user</h2>
              <p>
                The standalone shell only initializes NHSNLink after a simulated user has been selected. Use the shell controls on the left to answer
                “who are you simulating?” and then the UI will load the shared NHSNLink experience against the BFF.
              </p>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;