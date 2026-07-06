import React from 'react';

type RouteName = 'home' | 'users' | 'onboarding';

export interface NavigationItem {
  key: RouteName;
  label: string;
}

interface NavigationRailProps {
  title: string;
  items: NavigationItem[];
  activeRoute: RouteName;
  onNavigate: (route: RouteName) => void;
  userName: string;
  userEmail: string;
}

export function NavigationRail({
  title,
  items,
  activeRoute,
  onNavigate,
  userName,
  userEmail
}: NavigationRailProps) {
  return (
    <aside className="nhsn-link__nav">
      <div>
        <h1 className="nhsn-link__nav-title">{title}</h1>
        <h2>Navigation</h2>
      </div>
      <ul>
        {items.map(item => (
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