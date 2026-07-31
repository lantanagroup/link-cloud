import React from 'react';
import {createRoot} from 'react-dom/client';
import {App} from './App';
import {NotificationProvider} from '../components/notifications/NotificationProvider';
import {ensureI18nInitialized} from '../localization/i18n';
import '../styles.scss';

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

void ensureI18nInitialized({ apiBaseUrl: '/api' }).then(() => {
  createRoot(rootElement).render(
    <React.StrictMode>
      <NotificationProvider>
        <App />
      </NotificationProvider>
    </React.StrictMode>
  );
});
