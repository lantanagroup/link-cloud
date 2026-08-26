import React from 'react';
import {createRoot, Root} from 'react-dom/client';
import {AppRoot} from '../core/AppRoot';
import {BffApiClient} from '../core/api/BffApiClient';
import {ensureI18nInitialized} from '../core/localization/i18n';
import '../styles.scss';

/**
 * The embed entry point.
 *
 * Imports from `core/` only. Anything reachable from this file ends up in
 * `dist/embed/nhsn-link.js` and therefore runs inside the CDC NHSN App with
 * their user's session — which is why `shell/` must never appear in this
 * import graph. The bundle-boundary test asserts it on the built artifact.
 */
class NhsnLinkElement extends HTMLElement {
  private root?: Root;

  static get observedAttributes() {
    return ['baseurl', 'apibaseurl', 'locale'];
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
    const locale = this.getAttribute('locale') || undefined;

    // The composition root: core never constructs a client.
    const client = new BffApiClient(apiBaseUrl);

    void ensureI18nInitialized({apiBaseUrl, locale}).then(() => {
      this.root?.render(<AppRoot client={client} baseUrl={baseUrl} locale={locale} />);
    });
  }
}

if (!customElements.get('nhsn-link')) {
  customElements.define('nhsn-link', NhsnLinkElement);
}

export {NHSNLink} from '../core/NHSNLink';
