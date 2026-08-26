import React from 'react';
import {useTranslation} from 'react-i18next';

type RouteName = 'home' | 'onboarding' | 'configuration';

export interface NavigationItem {
  key: RouteName;
  label: string;
}

export interface NavigationSection {
  heading?: string;
  items: NavigationItem[];
}

interface NavigationRailProps {
  title: string;
  sections: NavigationSection[];
  activeRoute: RouteName;
  onNavigate: (route: RouteName) => void;
  userName: string;
  userEmail: string;
}

export function NavigationRail({
  title,
  sections,
  activeRoute,
  onNavigate,
  userName,
  userEmail
}: NavigationRailProps) {
  const {t} = useTranslation('common');

  return (
    <aside className="nhsn-link__nav">
      <div>
        <h1 className="nhsn-link__nav-title">{title}</h1>
        <h2>{t('navigation.title')}</h2>
      </div>
      <div className="nhsn-link__nav-sections">
        {sections.map((section, index) => (
          <div key={section.heading ?? `section-${index}`} className="nhsn-link__nav-section">
            {section.heading && <h3 className="nhsn-link__nav-section-heading">{section.heading}</h3>}
            <ul>
              {section.items.map(item => (
                <li key={item.key}>
                  <button
                    type="button"
                    className={`nhsn-link__nav-button${activeRoute === item.key ? ' nhsn-link__nav-button--active' : ''}`}
                    onClick={() => onNavigate(item.key)}>
                    {item.label}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      <div className="nhsn-link__nav-userinfo">
        <p>
          {userName}
          <br />
          {userEmail}
        </p>
      </div>
    </aside>
  );
}