import React from 'react';
import {createRoot, Root} from 'react-dom/client';
import {NHSNLink} from '../components/NHSNLink';
import {NotificationProvider} from '../components/notifications/NotificationProvider';
import '../styles.scss';

class NhsnLinkElement extends HTMLElement {
  private root?: Root;

  static get observedAttributes() {
    return ['baseurl', 'apibaseurl'];
  }

  attributeChangedCallback() {
    this.renderComponent();
  }

  connectedCallback() {
    if (!this.root) {
      this.root = createRoot(this);
    }

    this.renderComponent();
  }

  disconnectedCallback() {
    this.root?.unmount();
    this.root = undefined;
  }

  private renderComponent() {
    if (!this.root) {
      return;
    }

    const baseUrl = this.getAttribute('baseurl') || '/nhsnlink';
    const apiBaseUrl = this.getAttribute('apibaseurl') || '/api';
    this.root.render(
      <NotificationProvider>
        <NHSNLink baseUrl={baseUrl} apiBaseUrl={apiBaseUrl} />
      </NotificationProvider>
    );
  }
}

if (!customElements.get('nhsn-link')) {
  customElements.define('nhsn-link', NhsnLinkElement);
}

export { NHSNLink } from '../components/NHSNLink';