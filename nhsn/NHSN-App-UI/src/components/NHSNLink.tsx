import React, {useEffect, useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {UserInfoService} from '../services/user-info-service';
import {TestUserProfile, UserInfoResponse} from '../shared/models';
import {NavigationItem, NavigationRail, NavigationSection} from './NavigationRail';
import {ConfigurationScreen} from './ConfigurationScreen';
import {OnboardingScreen} from './OnboardingScreen';
import './NHSNLink.css';
import {setAppLocale} from '../localization/i18n';

export interface NHSNLinkProps {
  activeTestUser?: TestUserProfile;
  userInfoService?: UserInfoService;
  baseUrl?: string;
  apiBaseUrl?: string;
  locale?: string;
}

type RouteName = 'home' | 'onboarding' | 'configuration';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  onboarding: '/onboard',
  configuration: '/configuration'
};

export function NHSNLink({ activeTestUser, userInfoService, baseUrl = '/', apiBaseUrl = '/api', locale }: NHSNLinkProps) {
  const {t} = useTranslation('common');
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
    void setAppLocale(locale);
  }, [locale]);

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
          setError(err instanceof Error ? err.message : t('state.loadUserContextError'));
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
  }, [activeTestUser, effectiveUserInfoService, t]);

  const navigationSections = useMemo<NavigationSection[]>(() => {
    if (!userInfo || userInfo.AccessState !== 'Allowed') {
      return [];
    }

    const facilityItems: NavigationItem[] = [{ key: 'home', label: t('navigation.home') }];

    if (!userInfo.IsOnboarded) {
      facilityItems.push({ key: 'onboarding', label: t('navigation.onboarding') });
    }

    if (userInfo.IsOnboarded) {
      facilityItems.push({ key: 'configuration', label: t('navigation.configuration') });
    }

    return [{ heading: t('navigation.facility'), items: facilityItems }];
  }, [t, userInfo]);

  if (loading) {
    return <div className="nhsn-link__state">{t('state.loadingUserContext')}</div>;
  }

  if (error) {
    return <div className="nhsn-link__state">{error}</div>;
  }

  if (!userInfo) {
    return <div className="nhsn-link__state">{t('state.noUserContext')}</div>;
  }

  if (userInfo.AccessState === 'MissingRequiredRole') {
    return (
      <div className="nhsn-link__state">
        <div style={{ maxWidth: '600px', textAlign: 'center', padding: '1rem' }}>
          <h2>{t('auth.missingAccessTitle')}</h2>
          <p>{t('auth.missingAccessDescription')}</p>
          {userInfo.AccessRequestUrl && (
            <p>
              <a href={userInfo.AccessRequestUrl} target="_blank" rel="noreferrer">{t('actions.submitRequest')}</a>
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
          <h2>{t('auth.missingFacilityTitle')}</h2>
          <p>{t('auth.missingFacilityDescription')}</p>
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
          title={t('app.linkTitle')}
          sections={navigationSections}
          activeRoute={route}
          onNavigate={navigateTo}
          userName={userInfo.Name}
          userEmail={userInfo.Email} />

        <section className="nhsn-link__grid">
          {route === 'home' && (
            <>
              <div className="nhsn-link__panel">
                <h2>{t('home.userContextTitle')}</h2>
                <p><strong>{t('home.facilityLabel')}</strong> {userInfo.FacilityId ?? t('home.notAssigned')}</p>
                <p><strong>{t('home.groupsLabel')}</strong> {userInfo.Groups.length > 0 ? userInfo.Groups.join(', ') : t('home.noGroupsProvided')}</p>
                <p><strong>{t('home.accessStateLabel')}</strong> {userInfo.AccessState}</p>
                <p><strong>{t('home.facilityAdminLabel')}</strong> {userInfo.IsFacilityAdmin ? t('commonBoolean.yes') : t('commonBoolean.no')}</p>
                <p><strong>{t('home.onboardingLabel')}</strong> {userInfo.IsOnboarded ? t('home.onboardingComplete') : t('home.onboardingInProgress')}</p>
              </div>

              <div className="nhsn-link__panel">
                <h2>{t('home.frameworkStatusTitle')}</h2>
                {userInfo.IsOnboarded ? (
                  <p>
                    {t('home.maintenanceModeDescription')}
                  </p>
                ) : (
                  <p>
                    {t('home.onboardingModeDescription')}
                  </p>
                )}

                <h3>{t('home.availableNavigationTitle')}</h3>
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
