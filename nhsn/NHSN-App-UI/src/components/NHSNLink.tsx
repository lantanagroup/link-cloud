import React, {useEffect, useMemo, useState} from 'react';
import {UserInfoService} from '../services/user-info-service';
import {TestUserProfile, UserInfoResponse, UserRoleSummaryResponse} from '../shared/models';
import {NavigationItem, NavigationRail} from './NavigationRail';
import {OnboardingScreen} from './OnboardingScreen';
import {SystemAdminUsersScreen} from './SystemAdminUsersScreen';
import './NHSNLink.css';

export interface NHSNLinkProps {
  activeTestUser?: TestUserProfile;
  userInfoService?: UserInfoService;
  baseUrl?: string;
  apiBaseUrl?: string;
}

type RouteName = 'home' | 'users' | 'onboarding';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  users: '/admin/users',
  onboarding: '/onboard'
};

export function NHSNLink({ activeTestUser, userInfoService, baseUrl = '/', apiBaseUrl = '/api' }: NHSNLinkProps) {
  const effectiveUserInfoService = useMemo(() => userInfoService ?? new UserInfoService(apiBaseUrl), [userInfoService, apiBaseUrl]);
  const [userInfo, setUserInfo] = useState<UserInfoResponse | null>(null);
  const [users, setUsers] = useState<UserRoleSummaryResponse[]>([]);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingUserId, setSavingUserId] = useState<string | null>(null);
  const [route, setRoute] = useState<RouteName>('home');
  const normalizedBaseUrl = useMemo(() => normalizeBaseUrl(baseUrl), [baseUrl]);

  useEffect(() => {
    const syncRoute = () => {
      setRoute(resolveRoute(window.location.pathname, normalizedBaseUrl));
    };

    syncRoute();
    window.addEventListener('popstate', syncRoute);
    return () => window.removeEventListener('popstate', syncRoute);
  }, [normalizedBaseUrl]);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    setError(null);

    effectiveUserInfoService.getUserInfo(activeTestUser)
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
  }, [activeTestUser, effectiveUserInfoService]);

  const navigation = useMemo<NavigationItem[]>(() => {
    if (!userInfo) {
      return [];
    }

    if (userInfo.IsSystemAdmin) {
      return [
        { key: 'home' as RouteName, label: 'Home' },
        { key: 'users' as RouteName, label: 'Users' }
      ];
    }

    if (!userInfo.IsOnboarded) {
      return [
        { key: 'home' as RouteName, label: 'Home' },
        { key: 'onboarding' as RouteName, label: 'Onboarding' }
      ];
    }

    return [{ key: 'home' as RouteName, label: 'Home' }];
  }, [userInfo]);

  useEffect(() => {
    if (!userInfo) {
      return;
    }

    if (userInfo?.IsSystemAdmin) {
      if (route === 'onboarding') {
        navigateTo('home');
      }
      return;
    }

    if (route === 'users') {
      navigateTo('home');
    }
  }, [route, userInfo?.IsSystemAdmin, normalizedBaseUrl]);

  useEffect(() => {
    if (!activeTestUser || !userInfo?.IsSystemAdmin) {
      setUsers([]);
      setUsersError(null);
      return;
    }

    let cancelled = false;
    effectiveUserInfoService.getUsers(activeTestUser)
      .then(results => {
        if (!cancelled) {
          setUsers(results);
        }
      })
      .catch(loadError => {
        if (!cancelled) {
          setUsersError(loadError instanceof Error ? loadError.message : 'Unable to load users.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeTestUser, userInfo?.IsSystemAdmin, effectiveUserInfoService]);

  if (loading) {
    return <div className="nhsn-link__state">Loading NHSNLink user context�</div>;
  }

  if (error) {
    return <div className="nhsn-link__state">{error}</div>;
  }

  if (!userInfo) {
    return <div className="nhsn-link__state">No user context was returned.</div>;
  }

  if (!userInfo.IsActive) {
    return (
      <div className="nhsn-link__state">
        <div style={{ maxWidth: '600px', textAlign: 'center', padding: '1rem' }}>
          <h2>Your account does not have access to NHSNLink.</h2>
          <p>Submit a request to restore access.</p>
          {userInfo.AccessRequestUrl && (
            <p>
              <a href={userInfo.AccessRequestUrl} target="_blank" rel="noreferrer">Submit a request</a>
            </p>
          )}
        </div>
      </div>
    );
  }

  function navigateTo(nextRoute: RouteName) {
    const targetPath = buildPath(nextRoute, normalizedBaseUrl);
    if (window.location.pathname !== targetPath) {
      window.history.pushState({}, '', targetPath);
    }

    setRoute(nextRoute);
  }

  return (
    <div className="nhsn-link">
      <div className="nhsn-link__layout">
        <NavigationRail
          title="NHSNLink"
          items={navigation}
          activeRoute={route}
          onNavigate={navigateTo}
          userName={userInfo.Name}
          userEmail={userInfo.Email} />

        <section className="nhsn-link__grid">
          {route === 'home' && (
            <>
              <div className="nhsn-link__panel">
                <h2>User context</h2>
                <p><strong>Facility:</strong> {userInfo.FacilityId ?? 'Not assigned'}</p>
                <p><strong>Roles:</strong> {userInfo.Roles.length > 0 ? userInfo.Roles.join(', ') : 'No NHSNLink roles assigned yet'}</p>
                <p><strong>Groups:</strong> {userInfo.Groups.length > 0 ? userInfo.Groups.join(', ') : 'No groups provided'}</p>
                <p><strong>System administrator:</strong> {userInfo.IsSystemAdmin ? 'Yes' : 'No'}</p>
                <p><strong>Onboarding complete:</strong> {userInfo.IsSystemAdmin ? 'Not required for system administrators' : (userInfo.IsOnboarded ? 'Yes' : 'No')}</p>
              </div>

              <div className="nhsn-link__content">
                <h2>{userInfo.IsOnboarded ? 'Configuration maintenance' : 'Onboarding'}</h2>
                {!userInfo.IsSystemAdmin && !userInfo.IsOnboarded ? (
                  <>
                    <p>You must complete onboarding to continue...</p>
                    <button
                      type="button"
                      className="nhsn-link__action-button"
                      onClick={() => navigateTo('onboarding')}>
                      Begin Onboarding
                    </button>
                  </>
                ) : userInfo.IsOnboarded ? (
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
            </>
          )}

          {route === 'onboarding' && !userInfo.IsSystemAdmin && (
            <OnboardingScreen />
          )}

          {route === 'users' && activeTestUser && userInfo.IsSystemAdmin && (
            <SystemAdminUsersScreen
              activeTestUser={activeTestUser}
              currentUserEmail={userInfo.Email}
              userInfoService={effectiveUserInfoService}
              users={users}
              usersError={usersError}
              savingUserId={savingUserId}
              onUsersChanged={setUsers}
              onUsersErrorChanged={setUsersError}
              onSavingUserIdChanged={setSavingUserId} />
          )}
        </section>
      </div>
    </div>
  );
}

export default NHSNLink;

function normalizeBaseUrl(baseUrl: string): string {
  if (!baseUrl || baseUrl === '/') {
    return '/';
  }

  const withLeadingSlash = baseUrl.startsWith('/') ? baseUrl : `/${baseUrl}`;
  return withLeadingSlash.endsWith('/') ? withLeadingSlash.slice(0, -1) : withLeadingSlash;
}

function buildPath(route: RouteName, baseUrl: string): string {
  const normalizedBase = normalizeBaseUrl(baseUrl);
  const routePath = routePathMap[route];

  if (normalizedBase === '/') {
    return routePath;
  }

  return routePath === '/'
    ? normalizedBase
    : `${normalizedBase}${routePath}`;
}

function resolveRoute(pathname: string, baseUrl: string): RouteName {
  const normalizedBase = normalizeBaseUrl(baseUrl);
  const strippedPath = normalizedBase === '/'
    ? pathname || '/'
    : pathname.startsWith(normalizedBase)
      ? pathname.slice(normalizedBase.length) || '/'
      : '/';

  switch (strippedPath) {
    case '/admin/users':
      return 'users';
    case '/onboard':
      return 'onboarding';
    case '/':
    case '':
    default:
      return 'home';
  }
}
