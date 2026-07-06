import React from 'react';
import {createRoot} from 'react-dom/client';
import {App} from './App';
import {NotificationProvider} from '../components/notifications/NotificationProvider';

const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('Root element was not found.');
}

createRoot(rootElement).render(
  <React.StrictMode>
    <NotificationProvider>
      <App />
    </NotificationProvider>
  </React.StrictMode>
);