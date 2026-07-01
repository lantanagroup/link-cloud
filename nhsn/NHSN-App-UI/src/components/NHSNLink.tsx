import React, {useEffect, useMemo, useState} from 'react';
import {UserInfoService} from '../services/user-info-service';
import {TestUserProfile, UserInfoResponse} from '../shared/models';
import './NHSNLink.css';

export interface NHSNLinkProps {
  activeTestUser?: TestUserProfile;
  userInfoService?: UserInfoService;
}

const defaultService = new UserInfoService();

export function NHSNLink({ activeTestUser, userInfoService = defaultService }: NHSNLinkProps) {
  const [userInfo, setUserInfo] = useState<UserInfoResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    setError(null);

    userInfoService.getUserInfo(activeTestUser)
      .then(result => {
        if (!mounted) {
          return;
        }

        setUserInfo(result);
      })
      .catch(err => {
        if (!mounted) {
          return;
        }

        setError(err instanceof Error ? err.message : 'Unable to load user context.');
      })
      .finally(() => {
        if (mounted) {
          setLoading(false);
        }
      });

    return () => {
      mounted = false;
    };
  }, [activeTestUser, userInfoService]);

  const navigation = useMemo(() => {
    if (!userInfo) {
      return [];
    }

    if (!userInfo.IsOnboarded) {
      return ['Onboarding'];
    }

    return ['Configuration Overview', 'Configuration Changes', 'Maintenance'];
  }, [userInfo]);

  if (loading) {
    return <div className="nhsn-link__state">Loading NHSNLink user context…</div>;
  }

  if (error) {
    return <div className="nhsn-link__state">{error}</div>;
  }

  if (!userInfo) {
    return <div className="nhsn-link__state">No user context was returned.</div>;
  }

  return (
    <div className="nhsn-link">
      <header className="nhsn-link__header">
        <h1 className="nhsn-link__title">NHSNLink</h1>
        <div className="nhsn-link__subtitle">
          Signed in as <strong>{userInfo.Name}</strong> ({userInfo.Email})
        </div>
      </header>

      <div className="nhsn-link__layout">
        <aside className="nhsn-link__nav">
          <h2>Navigation</h2>
          <ul>
            {navigation.map(item => <li key={item}>{item}</li>)}
          </ul>
        </aside>

        <section className="nhsn-link__grid">
          <div className="nhsn-link__panel">
            <h2>User context</h2>
            <p><strong>Facility:</strong> {userInfo.FacilityId ?? 'Not assigned'}</p>
            <p><strong>Roles:</strong> {userInfo.Roles.length > 0 ? userInfo.Roles.join(', ') : 'No NHSNLink roles assigned yet'}</p>
            <p><strong>Groups:</strong> {userInfo.Groups.length > 0 ? userInfo.Groups.join(', ') : 'No groups provided'}</p>
            <p><strong>Onboarding complete:</strong> {userInfo.IsOnboarded ? 'Yes' : 'No'}</p>
          </div>

          <div className="nhsn-link__content">
            <h2>{userInfo.IsOnboarded ? 'Configuration maintenance' : 'Onboarding'}</h2>
            {userInfo.IsOnboarded ? (
              <p>
                This framework foundation is in maintenance mode. As the facility configuration evolves, this area can surface the
                specific onboarding artifacts or settings that need to be reviewed or updated.
              </p>
            ) : (
              <p>
                This framework foundation is in onboarding mode. Future work will guide the user through initial facility
                onboarding, source-system coordination, and role-appropriate setup tasks.
              </p>
            )}

            <h3>Available navigation from the BFF</h3>
            <ul>
              {userInfo.AvailableNavigation.map(item => <li key={item}>{item}</li>)}
            </ul>
          </div>
        </section>
      </div>
    </div>
  );
}

export default NHSNLink;