import React, {useEffect, useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from './api/ApiClientContext';
import type {UserInfoResponse} from './api/contracts';
import {NavigationItem, NavigationRail, NavigationSection} from './NavigationRail';
import {ConfigurationScreen} from './ConfigurationScreen';
import {OnboardingProvider} from './onboarding/OnboardingProvider';
import {StepHost} from './onboarding/StepHost';
import {normalizeBaseUrl} from './onboarding/navigation';
import './NHSNLink.css';
import {setAppLocale} from './localization/i18n';

export interface NHSNLinkProps {
  baseUrl?: string;
  /** Where "Return to Home" on the completed-enrollment screen sends the browser. Host page, not a route inside this app. */
  homeUrl?: string;
  locale?: string;
}

type RouteName = 'home' | 'onboarding' | 'configuration';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  onboarding: '/onboarding',
  configuration: '/configuration'
};

/**
 * The component both builds render.
 *
 * Takes no API client and no user: the client is injected at each entry
 * point's composition root, and the user context is server-observed. There is
 * deliberately no prop by which a caller can assert a facility or a role.
 */
export function NHSNLink({baseUrl = '/', homeUrl = '/', locale}: NHSNLinkProps) {
  const {t} = useTranslation('common');
  const api = useApiClient();
  const [userInfo, setUserInfo] = useState<UserInfoResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [route, setRoute] = useState<RouteName>('home');
  const normalizedBaseUrl = useMemo(() => normalizeBaseUrl(baseUrl), [baseUrl]);

  useEffect(() => {
    const syncRoute = () => setRoute(resolveRoute(window.location.pathname, normalizedBaseUrl));
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

    api
      .getUserInfo()
      .then(result => {
        if (mounted) {
          setUserInfo(result);
        }
      })
      .catch((cause: unknown) => {
        if (mounted) {
          setError(cause instanceof Error ? cause.message : t('errors.unexpected'));
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
  }, [api, t]);

  const navigationSections = useMemo<NavigationSection[]>(() => {
    if (!userInfo || userInfo.accessState !== 'Allowed') {
      return [];
    }

    const facilityItems: NavigationItem[] = [{key: 'home', label: t('navigation.home')}];

    if (!userInfo.isOnboarded) {
      facilityItems.push({key: 'onboarding', label: t('navigation.onboarding')});
    } else {
      facilityItems.push({key: 'configuration', label: t('navigation.configuration')});
    }

    return [{heading: t('navigation.facility'), items: facilityItems}];
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

  if (userInfo.accessState === 'MissingRequiredRole') {
    return (
      <div className="nhsn-link__state">
        <div className="nhsn-link__state-card">
          <h2>{t('auth.missingAccessTitle')}</h2>
          <p>{t('auth.missingAccessDescription')}</p>
          {userInfo.accessRequestUrl && (
            <p>
              <a href={userInfo.accessRequestUrl} target="_blank" rel="noreferrer">
                {t('actions.submitRequest')}
              </a>
            </p>
          )}
        </div>
      </div>
    );
  }

  if (userInfo.accessState === 'MissingFacility' || !userInfo.hasFacility) {
    return (
      <div className="nhsn-link__state">
        <div className="nhsn-link__state-card">
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
          userName={userInfo.name}
          userEmail={userInfo.email} />

        <section className="nhsn-link__grid">
          {route === 'home' && <HomePanels userInfo={userInfo} />}
          {route === 'onboarding' && !userInfo.isOnboarded && (
            <OnboardingProvider user={userInfo} baseUrl={normalizedBaseUrl} homeUrl={homeUrl}>
              <StepHost />
            </OnboardingProvider>
          )}

          {route === 'configuration' && userInfo.isOnboarded && <ConfigurationScreen />}
        </section>
      </div>
    </div>
  );
}

function HomePanels({userInfo}: {userInfo: UserInfoResponse}) {
  const {t} = useTranslation('common');
  return (
    <>
      <div className="nhsn-link__panel">
        <h2>{t('home.userContextTitle')}</h2>
        <p>
          <strong>{t('home.facilityLabel')}</strong>{' '}
          {userInfo.facilityId ?? t('home.notAssigned')}
        </p>
        <p>
          <strong>{t('home.groupsLabel')}</strong>{' '}
          {userInfo.groups.length > 0 ? userInfo.groups.join(', ') : t('home.noGroupsProvided')}
        </p>
        <p>
          <strong>{t('home.accessStateLabel')}</strong> {userInfo.accessState}
        </p>
        <p>
          <strong>{t('home.facilityAdminLabel')}</strong>{' '}
          {userInfo.isFacilityAdmin ? t('commonBoolean.yes') : t('commonBoolean.no')}
        </p>
        <p>
          <strong>{t('home.onboardingLabel')}</strong>{' '}
          {userInfo.isOnboarded ? t('home.onboardingComplete') : t('home.onboardingInProgress')}
        </p>
      </div>

      <div className="nhsn-link__panel">
        <h2>{t('home.frameworkStatusTitle')}</h2>
        <p>
          {userInfo.isOnboarded
            ? t('home.maintenanceModeDescription')
            : t('home.onboardingModeDescription')}
        </p>

        <h3>{t('home.availableNavigationTitle')}</h3>
        <ul>
          {userInfo.availableNavigation.map(item => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      </div>
    </>
  );
}

export default NHSNLink;

function resolveRoute(pathname: string, baseUrl: string): RouteName {
  const withoutBase =
    baseUrl !== '/' && pathname.startsWith(baseUrl) ? pathname.slice(baseUrl.length) || '/' : pathname;

  if (withoutBase === routePathMap.onboarding || withoutBase.startsWith(`${routePathMap.onboarding}/`)) {
    return 'onboarding';
  }
  if (withoutBase === routePathMap.configuration) {
    return 'configuration';
  }
  return 'home';
}

function buildPath(route: RouteName, baseUrl: string): string {
  const routePath = routePathMap[route];
  if (baseUrl === '/') {
    return routePath;
  }
  return routePath === '/' ? baseUrl : `${baseUrl}${routePath}`;
}
