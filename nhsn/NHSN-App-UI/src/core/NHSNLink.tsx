import React, { useEffect, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useApiClient } from './api/ApiClientContext';
import {
  NavigationItem,
  NavigationRail,
  NavigationSection,
} from './NavigationRail';
import { ConfigurationScreen } from './ConfigurationScreen';
import { Home } from './Home';
import { OnboardingProvider } from './onboarding/OnboardingProvider';
import { OnboardingStepsNav, StepHost } from './onboarding/StepHost';
import { normalizeBaseUrl } from './onboarding/navigation';
import './NHSNLink.css';
import { setAppLocale } from './localization/i18n';

export interface NHSNLinkProps {
  baseUrl?: string;
  locale?: string;
}

type RouteName = 'home' | 'onboarding' | 'configuration';

const routePathMap: Record<RouteName, string> = {
  home: '/',
  onboarding: '/onboarding',
  configuration: '/configuration',
};

/**
 * The component both builds render.
 *
 * Takes no API client and no user: the client is injected at each entry
 * point's composition root, and the user context is server-observed. There is
 * deliberately no prop by which a caller can assert a facility or a role.
 */
export function NHSNLink({ baseUrl = '/', locale }: NHSNLinkProps) {
  const { t } = useTranslation('common');
  const api = useApiClient();
  const queryClient = useQueryClient();
  const {
    data: userInfo,
    isLoading: loading,
    error: userInfoError,
  } = useQuery({
    queryKey: ['userInfo'],
    queryFn: () => api.getUserInfo(),
    staleTime: Infinity,
  });
  const error = userInfoError
    ? userInfoError instanceof Error
      ? userInfoError.message
      : t('errors.unexpected')
    : null;
  const [route, setRoute] = useState<RouteName>('home');
  const normalizedBaseUrl = useMemo(() => normalizeBaseUrl(baseUrl), [baseUrl]);

  useHintTooltips();

  useEffect(() => {
    const syncRoute = () =>
      setRoute(resolveRoute(window.location.pathname, normalizedBaseUrl));
    syncRoute();
    window.addEventListener('popstate', syncRoute);
    return () => window.removeEventListener('popstate', syncRoute);
  }, [normalizedBaseUrl]);

  useEffect(() => {
    void setAppLocale(locale);
  }, [locale]);

  const navigationSections = useMemo<NavigationSection[]>(() => {
    if (!userInfo || userInfo.accessState !== 'Allowed') {
      return [];
    }

    const facilityItems: NavigationItem[] = userInfo.isOnboarded
      ? [
          { key: 'home', label: t('navigation.home') },
          { key: 'configuration', label: t('navigation.configuration') },
        ]
      : [{ key: 'onboarding', label: t('navigation.onboarding') }];

    return [{ heading: t('navigation.facility'), items: facilityItems }];
  }, [t, userInfo]);

  if (loading) {
    return (
      <div className="nhsn-link__state">{t('state.loadingUserContext')}</div>
    );
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
              <a
                href={userInfo.accessRequestUrl}
                target="_blank"
                rel="noreferrer">
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

  const showOnboarding = !userInfo.isOnboarded;

  return (
    <div className="nhsn-link">
      {showOnboarding ? (
        <OnboardingProvider
          user={userInfo}
          baseUrl={normalizedBaseUrl}
          onGoHome={async () => {
            // isOnboarded flipping true happens server-side; the staleTime: Infinity
            // userInfo query above never learns that on its own. Re-checking it here
            // is the gate: only navigate once it actually reports onboarded, otherwise
            // stay on the current step.
            try {
              const refreshed = await api.getUserInfo();
              queryClient.setQueryData(['userInfo'], refreshed);
              if (refreshed.isOnboarded) {
                navigateTo('home');
              }
            } catch {
              // Could not confirm onboarding status -- stay put rather than navigate.
            }
          }}>
          <div className="nhsn-link__layout">
            <NavigationRail
              title={t('app.linkTitle')}
              sections={[]}
              activeRoute={route}
              onNavigate={navigateTo}
              userName={userInfo.name}
              userEmail={userInfo.email}
              facilityName={userInfo.facilityName}
              facilityId={userInfo.facilityId}
              stepsSection={<OnboardingStepsNav />}
            />

            <main className="nhsn-link__grid">
              <StepHost />
            </main>
          </div>
        </OnboardingProvider>
      ) : (
        <div className="nhsn-link__layout">
          <NavigationRail
            title={t('app.linkTitle')}
            sections={navigationSections}
            activeRoute={route}
            onNavigate={navigateTo}
            userName={userInfo.name}
            userEmail={userInfo.email}
            facilityName={userInfo.facilityName}
            facilityId={userInfo.facilityId}
          />

          <main className="nhsn-link__grid">
            {route === 'home' && <Home userInfo={userInfo} />}
            {route === 'configuration' && userInfo.isOnboarded && (
              <ConfigurationScreen />
            )}
          </main>
        </div>
      )}
    </div>
  );
}

export default NHSNLink;

function resolveRoute(pathname: string, baseUrl: string): RouteName {
  const withoutBase =
    baseUrl !== '/' && pathname.startsWith(baseUrl)
      ? pathname.slice(baseUrl.length) || '/'
      : pathname;

  if (
    withoutBase === routePathMap.onboarding ||
    withoutBase.startsWith(`${routePathMap.onboarding}/`)
  ) {
    return 'onboarding';
  }
  if (withoutBase === routePathMap.configuration) {
    return 'configuration';
  }
  return 'home';
}

const FIELD_HINT_OPEN_CLASS = 'k-form-field--hint-open';
const INFO_ICON_OPEN_CLASS = 'info-icon--open';
const OPEN_SELECTOR = `.${FIELD_HINT_OPEN_CLASS}, .${INFO_ICON_OPEN_CLASS}`;
const HINT_LABEL_SELECTOR = '.k-form-field:has(.k-form-hint) .k-label';

function closeAllHintsExcept(keep: Element | null) {
  document.querySelectorAll(OPEN_SELECTOR).forEach((el) => {
    if (el !== keep) {
      el.classList.remove(FIELD_HINT_OPEN_CLASS, INFO_ICON_OPEN_CLASS);
    }
  });
}

function toggleFieldHint(field: Element) {
  const isOpen = field.classList.contains(FIELD_HINT_OPEN_CLASS);
  closeAllHintsExcept(isOpen ? null : field);
  field.classList.toggle(FIELD_HINT_OPEN_CLASS, !isOpen);
}

/**
 * The "?" badge is CSS-generated content (`.k-label::after`) and can never be
 * focusable, so this injects a real `<button>` into each hinted label instead.
 * Kept in sync via a MutationObserver since steps mount new fields on navigation.
 */
function useHintLabelFocusability() {
  useEffect(() => {
    function tagLabels(root: ParentNode) {
      root.querySelectorAll<HTMLLabelElement>(HINT_LABEL_SELECTOR).forEach((label) => {
        if (label.dataset.hintTrigger === 'true') {
          return;
        }
        label.dataset.hintTrigger = 'true';

        const hintText = label.htmlFor
          ?     document.getElementById(`${label.htmlFor}_hint`)?.textContent ?? ''
          : '';
        const bubbleId = label.htmlFor ? `${label.htmlFor}_hint_bubble` : undefined;

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'nhsn-link__hint-trigger';
        button.setAttribute('aria-label', 'More information');
        if (bubbleId) {
          button.setAttribute('aria-describedby', bubbleId);
        }
        button.appendChild(document.createTextNode('?'));

        const bubble = document.createElement('span');
        bubble.className = 'nhsn-link__hint-trigger-bubble';
        bubble.setAttribute('role', 'tooltip');
        if (bubbleId) {
          bubble.id = bubbleId;
        }
        bubble.textContent = hintText;
        button.appendChild(bubble);

        label.appendChild(button);
      });
    }

    tagLabels(document);
    const observer = new MutationObserver((mutations) => {
      if (mutations.some((mutation) => mutation.addedNodes.length > 0)) {
        tagLabels(document);
      }
    });
    observer.observe(document.body, {childList: true, subtree: true});
    return () => observer.disconnect();
  }, []);
}

function useHintTooltips() {
  useHintLabelFocusability();
  useEffect(() => {
    function handleClick(event: MouseEvent) {
      const target = event.target as Element | null;
      if (!target) {
        return;
      }

      const label = target.closest('.k-label');
      const field = label?.closest('.k-form-field') ?? null;
      const fieldTrigger = field?.querySelector('.k-form-hint') ? field : null;
      const infoIconTrigger = target.closest('.info-icon');

      const trigger = fieldTrigger ?? infoIconTrigger;
      if (!trigger) {
        if (!target.closest('.k-form-hint, .tooltip-bubble')) {
          closeAllHintsExcept(null);
        }
        return;
      }

      event.preventDefault();
      event.stopPropagation();

      if (fieldTrigger) {
        toggleFieldHint(fieldTrigger);
      } else {
        const isOpen = trigger.classList.contains(INFO_ICON_OPEN_CLASS);
        closeAllHintsExcept(isOpen ? null : trigger);
        trigger.classList.toggle(INFO_ICON_OPEN_CLASS, !isOpen);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        closeAllHintsExcept(null);
      }
    }

    document.addEventListener('click', handleClick, true);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('click', handleClick, true);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);
}

function buildPath(route: RouteName, baseUrl: string): string {
  const routePath = routePathMap[route];
  if (baseUrl === '/') {
    return routePath;
  }
  return routePath === '/' ? baseUrl : `${baseUrl}${routePath}`;
}
