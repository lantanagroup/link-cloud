import React, {useEffect, useMemo, useState} from 'react';
import {UserInfoService} from '../services/user-info-service';
import {TestUserProfile, UserInfoResponse} from '../shared/models';
import {NavigationItem, NavigationRail, NavigationSection} from './NavigationRail';
import {ConfigurationScreen} from './ConfigurationScreen';
import {OnboardingScreen} from './OnboardingScreen';
import './NHSNLink.css';

export interface NHSNLinkProps {
  activeTestUser?: TestUserProfile;
  userInfoService?: UserInfoService;
  baseUrl?: string;
  apiBaseUrl?: string;
}

type RouteName = 'home' | 'onboarding' | 'configuration';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  onboarding: '/onboard',
  configuration: '/configuration'
};

export function NHSNLink({ activeTestUser, userInfoService, baseUrl = '/', apiBaseUrl = '/api' }: NHSNLinkProps) {
  const effectiveUserInfoService = useMemo(() => userInfoService ?? new UserInfoService(apiBaseUrl), [userInfoService, apiBaseUrl]);
  const [userInfo, setUserInfo] = useState<UserInfoResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
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
        if (mounted) {
          setUserInfo(result);
        }
      })
      .catch(err => {
        if (mounted) {
          setError(err instanceof Error ? err.message : 'Unable to load user context.');
        }
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
    if (!userInfo || userInfo.AccessState !== 'Allowed') {
      return [];
    }

    const facilityItems: NavigationItem[] = [{ key: 'home', label: 'Home' }];

    if (!userInfo.IsOnboarded) {
      facilityItems.push({ key: 'onboarding', label: 'Onboarding' });
    }

    if (userInfo.IsOnboarded) {
      facilityItems.push({ key: 'configuration', label: 'Configuration' });
    }

    return [{ heading: 'Facility', items: facilityItems }];
  }, [userInfo]);

  if (loading) {
    return <div className="nhsn-link__state">Loading NHSNLink user context...</div>;
  }

  if (error) {
    return <div className="nhsn-link__state">{error}</div>;
  }

  if (!userInfo) {
    return <div className="nhsn-link__state">No user context was returned.</div>;
  }

  if (userInfo.AccessState === 'MissingRequiredRole') {
    return (
      <div className="nhsn-link__state">
        <div style={{ maxWidth: '600px', textAlign: 'center', padding: '1rem' }}>
          <h2>You do not currently have access to NHSNLink configuration.</h2>
          <p>Your NHSN App identity does not include the FACADMIN role required for this experience.</p>
          {userInfo.AccessRequestUrl && (
            <p>
              <a href={userInfo.AccessRequestUrl} target="_blank" rel="noreferrer">Submit a request</a>
            </p>
          )}
        </div>
      </div>
    );
  }

  if (userInfo.AccessState === 'MissingFacility' || !userInfo.HasFacility) {
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
                <p><strong>Groups:</strong> {userInfo.Groups.length > 0 ? userInfo.Groups.join(', ') : 'No groups provided'}</p>
                <p><strong>Access state:</strong> {userInfo.AccessState}</p>
                <p><strong>Facility admin:</strong> {userInfo.IsFacilityAdmin ? 'Yes' : 'No'}</p>
                <p><strong>Onboarding:</strong> {userInfo.IsOnboarded ? 'Complete' : 'In progress'}</p>
              </div>

              <div className="nhsn-link__panel">
                <h2>Framework status</h2>
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
            </>
          )}

          {route === 'onboarding' && !userInfo.IsOnboarded && activeTestUser && userInfo.FacilityId && (
            <OnboardingScreen
              activeTestUser={activeTestUser}
              facilityId={userInfo.FacilityId}
              userInfoService={effectiveUserInfoService}
              onCompleted={() => window.location.reload()} />
          )}

          {route === 'configuration' && userInfo.IsOnboarded && (
            <ConfigurationScreen />
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
