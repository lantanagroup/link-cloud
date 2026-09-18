import React from 'react';
import {render, screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {beforeEach, describe, expect, it} from 'vitest';
import {AppRoot} from '../../../AppRoot';
import {MockApiClient} from '../../../../shell/mocks/MockApiClient';
import {ensureI18nInitialized} from '../../../localization/i18n';
import {createEmptyDraft} from '../../types';

/**
 * Mounted through AppRoot + MockApiClient, matching walking-skeleton.test.tsx,
 * so the port (not fetch interception) exercises the real query path.
 */
describe('CensusStep', () => {
  beforeEach(async () => {
    await ensureI18nInitialized();
    window.localStorage.clear();

    const draft = createEmptyDraft();
    draft.currentStepId = 'census';
    draft.unlockedStepIds = ['welcome', 'reporting-plan', 'facility-info', 'manual-upload', 'fhir', 'census'];
    draft.facilityInfo = {timeZone: 'America/Chicago', vendor: 'Epic'};
    window.localStorage.setItem('nhsn-app-ui.mockDraft.MOCK-FACILITY-001', JSON.stringify(draft));
    window.history.pushState({}, '', '/onboarding/census');
  });

  const listLabels = [
    'Admit - Within 24 Hours',
    'Admit - Between 24 and 48 hours',
    'Admit - Over 48 hours',
    'Discharge - Where lookback of admission is within past 24 hours',
    'Discharge - Where lookback of admission is between 24 and 48 hours',
    'Discharge - Where lookback of admission is over 48 hours'
  ];

  it('validates all six patient lists and previews the selected one', async () => {
    const user = userEvent.setup();
    render(
      <AppRoot
        client={new MockApiClient(undefined, undefined, {
          patientListWithNames: true,
          fhirConnectionProbe: false,
          sftpFileListing: false,
          onboardingRevisit: false
        })}
        baseUrl="/"
      />
    );

    for (const label of listLabels) {
      const input = await screen.findByLabelText(label);
      await user.type(input, 'list-abc');
    }
    await user.type(await screen.findByLabelText('Hours'), '0');
    await user.type(await screen.findByLabelText('Minutes'), '15');

    await user.click(await screen.findByRole('button', {name: 'Validate Census Results'}));

    const viewButton = await screen.findByRole('button', {name: `View results for ${listLabels[0]}`});
    await waitFor(() => expect(viewButton.hasAttribute('disabled')).toBe(false));
    await user.click(viewButton);

    await waitFor(() => screen.getByText('List Results Preview'));
  });

  it('hides the fetch/view UI and still completes the step when patientListWithNames is off', async () => {
    const user = userEvent.setup();
    render(<AppRoot client={new MockApiClient()} baseUrl="/" />);

    for (const label of listLabels) {
      const input = await screen.findByLabelText(label);
      await user.type(input, 'list-abc');
    }
    await user.type(await screen.findByLabelText('Hours'), '0');
    await user.type(await screen.findByLabelText('Minutes'), '15');

    expect(screen.queryByRole('button', {name: 'Validate Census Results'})).toBeNull();
    expect(screen.queryByRole('button', {name: `View results for ${listLabels[0]}`})).toBeNull();
    expect(screen.queryByLabelText(/accuracy/i)).toBeNull();

    await user.click(await screen.findByRole('button', {name: 'Continue'}));

    await waitFor(() => {
      expect(window.location.pathname).toBe('/onboarding/location-org');
    });
  });
});

describe('CensusStep - Cerner', () => {
  beforeEach(async () => {
    await ensureI18nInitialized();
    window.localStorage.clear();

    const draft = createEmptyDraft();
    draft.currentStepId = 'census';
    draft.unlockedStepIds = ['welcome', 'reporting-plan', 'facility-info', 'manual-upload', 'fhir', 'census'];
    draft.facilityInfo = {timeZone: 'America/Chicago', vendor: 'Cerner'};
    window.localStorage.setItem('nhsn-app-ui.mockDraft.MOCK-FACILITY-001', JSON.stringify(draft));
    window.history.pushState({}, '', '/onboarding/census');
  });

  it('requires a successful connection test before Continue, regardless of sftpFileListing', async () => {
    const user = userEvent.setup();
    render(<AppRoot client={new MockApiClient()} baseUrl="/" />);

    await user.type(await screen.findByLabelText('SFTP Host'), 'sftp.example.invalid');
    await user.type(await screen.findByLabelText('SFTP Host URL Port'), '22');
    await user.type(await screen.findByLabelText('Hours'), '0');
    await user.type(await screen.findByLabelText('Minutes'), '15');

    const continueButton = await screen.findByRole('button', {name: 'Continue'});
    expect(continueButton.hasAttribute('disabled')).toBe(true);
    expect(screen.queryByText('SFTP Acquisition Results')).toBeNull();

    await user.click(await screen.findByRole('button', {name: 'Test Connection'}));
    await waitFor(() => expect(continueButton.hasAttribute('disabled')).toBe(false));

    // sftpFileListing is off by default, so the fixture-backed file listing stays hidden
    // even though the (real) connection test just succeeded.
    expect(screen.queryByText('SFTP Acquisition Results')).toBeNull();

    await user.click(continueButton);
    await waitFor(() => {
      expect(window.location.pathname).toBe('/onboarding/location-org');
    });
  });
});
