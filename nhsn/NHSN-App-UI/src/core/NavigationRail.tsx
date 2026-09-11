import React from 'react';

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
  facilityName?: string;
  facilityId?: string;
  stepsSection?: React.ReactNode;
}

/**
 * The app's single sidebar: identity header (never scrolls) plus a scrolling
 * body that is either the plain route buttons or, during onboarding, the step
 * rail — never both, so the two navigation surfaces this replaced can't stack.
 */
export function NavigationRail({
  title,
  sections,
  activeRoute,
  onNavigate,
  userName,
  userEmail,
  facilityName,
  facilityId,
  stepsSection
}: NavigationRailProps) {
  return (
    <aside className="nhsn-link__nav">
      <div className="nhsn-link__nav-header">
        <h1 className="nhsn-link__nav-title">{title}</h1>
        <div className="nhsn-link__nav-userinfo">
          <p>
            {userName}
            <br />
            {userEmail}
          </p>
        </div>
        {(facilityName || facilityId) && (
          <div className="nhsn-link__step-nav-facility">
            {facilityName && <div className="nhsn-link__step-nav-facility-name">{facilityName}</div>}
            {facilityId && <div className="nhsn-link__step-nav-facility-id">{facilityId}</div>}
          </div>
        )}
      </div>

      <div className="nhsn-link__nav-scroll">
        {stepsSection ?? (
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
        )}
      </div>
    </aside>
  );
}