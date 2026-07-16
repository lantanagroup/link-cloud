import React, {useEffect, useMemo, useState} from 'react';
import {UserInfoService} from '../services/user-info-service';
import {FacilitySummaryResponse, TestUserProfile, UserInfoResponse, UserRoleSummaryResponse} from '../shared/models';
import {NavigationItem, NavigationRail, NavigationSection} from './NavigationRail';
import {ConfigurationScreen} from './ConfigurationScreen';
import {OnboardingScreen} from './OnboardingScreen';
import {SystemAdminFacilitiesScreen} from './SystemAdminFacilitiesScreen';
import {SystemAdminUsersScreen} from './SystemAdminUsersScreen';
import './NHSNLink.css';

export interface NHSNLinkProps {
  activeTestUser?: TestUserProfile;
  userInfoService?: UserInfoService;
  baseUrl?: string;
  apiBaseUrl?: string;
}

type RouteName = 'home' | 'users' | 'onboarding' | 'configuration' | 'facilities';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  users: '/admin/users',
  facilities: '/admin/facilities',
  onboarding: '/onboard',
  configuration: '/configuration'
};

export function NHSNLink({ activeTestUser, userInfoService, baseUrl = '/', apiBaseUrl = '/api' }: NHSNLinkProps) {
  const effectiveUserInfoService = useMemo(() => userInfoService ?? new UserInfoService(apiBaseUrl), [userInfoService, apiBaseUrl]);
  const [userInfo, setUserInfo] = useState<UserInfoResponse | null>(null);
  const [users, setUsers] = useState<UserRoleSummaryResponse[]>([]);
  const [usersError, setUsersError] = useState<string | null>(null);
  const [facilities, setFacilities] = useState<FacilitySummaryResponse[]>([]);
  const [facilitiesError, setFacilitiesError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [savingUserId, setSavingUserId] = useState<string | null>(null);
  const [savingFacilityId, setSavingFacilityId] = useState<string | null>(null);
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

  const navigationSections = useMemo<NavigationSection[]>(() => {
    if (!userInfo) {
      return [];
    }

    const facilityItems: NavigationItem[] = [{ key: 'home' as RouteName, label: 'Home' }];
    const hasFacility = userInfo.HasFacility;
    const isFacilityAdmin = userInfo.Groups.includes('FACADMIN');

    if (hasFacility && isFacilityAdmin) {
      if (!userInfo.IsOnboarded) {
        facilityItems.push({ key: 'onboarding' as RouteName, label: 'Onboarding' });
      } else {
        facilityItems.push({ key: 'configuration' as RouteName, label: 'Configuration' });
      }
    }

    if (userInfo.IsSystemAdmin) {
      const sections: NavigationSection[] = [];
      if (hasFacility) {
        sections.push({ items: facilityItems });
      } else {
        sections.push({ items: [{ key: 'home' as RouteName, label: 'Home' }] });
      }

      sections.push({
        heading: 'Administration',
        items: [
          { key: 'users' as RouteName, label: 'Users' },
          { key: 'facilities' as RouteName, label: 'Facilities' }
        ]
      });

      return sections;
    }

    return [{ items: facilityItems }];
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

    if (route === 'users' || route === 'facilities') {
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

  useEffect(() => {
    if (!activeTestUser || !userInfo?.IsSystemAdmin) {
      setFacilities([]);
      setFacilitiesError(null);
      return;
    }

    let cancelled = false;
    effectiveUserInfoService.getFacilities(activeTestUser)
      .then(results => {
        if (!cancelled) {
          setFacilities(results);
        }
      })
      .catch(loadError => {
        if (!cancelled) {
          setFacilitiesError(loadError instanceof Error ? loadError.message : 'Unable to load facilities.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeTestUser, userInfo?.IsSystemAdmin, effectiveUserInfoService]);

  if (loading) {
    return <div className="nhsn-link__state">Loading NHSNLink user context...</div>;
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

  if (!userInfo.HasFacility) {
    return (
      <div className="nhsn-link__state">
        <div style={{ maxWidth: '600px', textAlign: 'center', padding: '1rem' }}>
          <h2>You must select a facility before proceeding.</h2>
          <p>Your user context did not include a facility. Please return to the NHSN App and choose a facility before continuing.</p>
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
      {userInfo.IsLowerEnvironmentTestingMode && (
        <div className="nhsn-link__testing-banner" role="alert">
          This system is configured for lower environment testing. Do not use this configuration in production.
        </div>
      )}
      <div className="nhsn-link__layout">
        <NavigationRail
          title="NHSNLink"
          sections={navigationSections}
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
                {!userInfo.IsSystemAdmin && userInfo.Groups.includes('FACADMIN') && !userInfo.IsOnboarded ? (
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

          {route === 'onboarding' && !userInfo.IsSystemAdmin && userInfo.Groups.includes('FACADMIN') && !userInfo.IsOnboarded && activeTestUser && userInfo.FacilityId && (
            <OnboardingScreen
              activeTestUser={activeTestUser}
              facilityId={userInfo.FacilityId}
              userInfoService={effectiveUserInfoService}
              onCompleted={() => window.location.reload()} />
          )}

          {route === 'configuration' && !userInfo.IsSystemAdmin && userInfo.Groups.includes('FACADMIN') && userInfo.IsOnboarded && (
            <ConfigurationScreen />
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

          {route === 'facilities' && activeTestUser && userInfo.IsSystemAdmin && (
            <SystemAdminFacilitiesScreen
              activeTestUser={activeTestUser}
              facilities={facilities}
              facilitiesError={facilitiesError}
              savingFacilityId={savingFacilityId}
              userInfoService={effectiveUserInfoService}
              onFacilitiesChanged={setFacilities}
              onFacilitiesErrorChanged={setFacilitiesError}
              onSavingFacilityIdChanged={setSavingFacilityId} />
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

  const trimmed = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

function resolveRoute(pathname: string, baseUrl: string): RouteName {
  const withoutBase = baseUrl !== '/' && pathname.startsWith(baseUrl)
    ? pathname.slice(baseUrl.length) || '/'
    : pathname;

  const normalizedPath = withoutBase || '/';

  const match = (Object.entries(routePathMap) as Array<[RouteName, string]>).find(([, path]) => path === normalizedPath);
  return match?.[0] ?? 'home';
}

function buildPath(route: RouteName, baseUrl: string): string {
  const routePath = routePathMap[route];
  if (baseUrl === '/') {
    return routePath;
  }

  return routePath === '/' ? baseUrl : `${baseUrl}${routePath}`;
}
