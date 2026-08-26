import React from 'react';
import {createRoot} from 'react-dom/client';
import {FacilityHarness} from '../shell/facilities/FacilityHarness';
import {addShellStrings} from '../shell/localization';
import {ensureI18nInitialized} from '../core/localization/i18n';
import '../styles.scss';

/**
 * The standalone entry point — `shell` + `core`.
 *
 * The only entry point permitted to import `shell/`. It mints real signed JWTs
 * from saved test profiles so lower-environment testing hits the same
 * validation path as production.
 */
declare global {
  interface Window {
    __NHSN_APP_UI_CONFIG__?: {
      defaultJwtIssuer?: string;
      defaultJwtKeyId?: string;
      defaultJwtPrivateKeyPem?: string;
    };
  }
}

const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('Root element was not found.');
}

void ensureI18nInitialized({apiBaseUrl: '/api'}).then(() => {
  // After init so the bundle is registered against a live i18n instance, and
  // non-overwriting so served strings still take precedence.
  addShellStrings();

  createRoot(rootElement).render(
    <React.StrictMode>
      <FacilityHarness />
    </React.StrictMode>
  );
});
