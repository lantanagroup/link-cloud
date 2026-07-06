import React from 'react';
import {createRoot, Root} from 'react-dom/client';
import {NHSNLink} from '../components/NHSNLink';
import {NotificationProvider} from '../components/notifications/NotificationProvider';
import '../styles.scss';

class NhsnLinkElement extends HTMLElement {
  private root?: Root;

  connectedCallback() {
    if (!this.root) {
      this.root = createRoot(this);
    }

    this.root.render(
      <NotificationProvider>
        <NHSNLink />
      </NotificationProvider>
    );
  }

  disconnectedCallback() {
    this.root?.unmount();
    this.root = undefined;
  }
}

if (!customElements.get('nhsn-link')) {
  customElements.define('nhsn-link', NhsnLinkElement);
}

export { NHSNLink } from '../components/NHSNLink';