import React from 'react';
import {render, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {beforeEach, describe, expect, it} from 'vitest';
import {AppRoot} from '../AppRoot';
import {MockApiClient} from '../../shell/mocks/MockApiClient';
import {ensureI18nInitialized} from '../localization/i18n';

/**
 * Proves the machine turns over: flow -> gating -> provider -> StepHost ->
 * fields -> ApiClient -> draft save -> URL sync.
 *
 * The port is what makes this cheap — MockApiClient is injected at the
 * composition root, so there is no fetch interception and no network.
 *
 * This test lives in `core` but imports from `shell`, which the lint rule
 * forbids for production code. That is intentional and safe: test files are
 * not on either entry point's import graph, so nothing here reaches a bundle.
 */
describe('onboarding walking skeleton', () => {
  beforeEach(async () => {
    await ensureI18nInitialized();
    window.localStorage.clear();
    window.history.pushState({}, '', '/onboarding/welcome');
  });

  function renderApp() {
    return render(<AppRoot client={new MockApiClient()} baseUrl="/" />);
  }

  /**
   * The navigation rail and the step rail are both lists, and PageHeader
   * renders its title more than once, so queries here are scoped to the step
   * machine's own class names rather than to roles or text.
   */
  const stepLabels = (container: HTMLElement) =>
    Array.from(container.querySelectorAll('.nhsn-link__steps .nhsn-link__step-label')).map(
      node => node.textContent
    );

  const currentStepLabel = (container: HTMLElement) =>
    container.querySelector('.nhsn-link__step--current .nhsn-link__step-label')?.textContent;

  it('renders the welcome step with all thirteen steps in the rail', async () => {
    const {container} = renderApp();

    await waitFor(() => expect(stepLabels(container)).toHaveLength(13));

    // Thirteen steps, reporting plan second — the POC's order.
    const labels = stepLabels(container);
    expect(labels[0]).toBe('Welcome');
    expect(labels[1]).toBe('Facility Reporting Plan');
    expect(labels[12]).toBe('Enrollment Complete');
    expect(currentStepLabel(container)).toBe('Welcome');
  });

  it('advances to the next step, mirrors it to the URL and persists the draft', async () => {
    const user = userEvent.setup();
    const {container} = renderApp();

    // Wait for the initial draft load to settle before acting. Clicking while
    // it is still in flight tests a race, not the transition.
    await waitFor(() => expect(stepLabels(container)).toHaveLength(13));

    await user.click(await screen.findByRole('button', {name: 'Continue'}));

    // The lazily-loaded next step resolves...
    await waitFor(() => expect(currentStepLabel(container)).toBe('Facility Reporting Plan'));

    // ...the URL follows the machine...
    await waitFor(() => {
      expect(window.location.pathname).toBe('/onboarding/reporting-plan');
    });

    // ...and the transition was saved, so a reload agrees with where we are.
    await waitFor(() => {
      const saved = window.localStorage.getItem('nhsn-app-ui.mockDraft');
      expect(saved).toBeTruthy();
      expect(JSON.parse(saved as string).currentStepId).toBe('reporting-plan');
    });
  });

  it('gates a deep link to a step the draft has not unlocked', async () => {
    window.history.pushState({}, '', '/onboarding/complete');
    const {container} = renderApp();

    // resolveStep rejects it and falls back to the furthest legal step, and
    // the URL is corrected rather than left telling a lie.
    await waitFor(() => expect(currentStepLabel(container)).toBe('Welcome'));
    await waitFor(() => {
      expect(window.location.pathname).toBe('/onboarding/welcome');
    });
  });
});
