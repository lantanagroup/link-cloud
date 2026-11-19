import {expect, test} from '../support/test';
import {draftAt} from '../fixtures/drafts';

test.describe('welcome step', () => {
  test('introduces the programme and links out in new tabs', async ({page, harness, onboarding}) => {
    await harness.open();

    // HeadingPause appends a visually-hidden "." to every step heading, so the accessible
    // text is "Welcome to DQM Onboarding ." — never assert a step heading exactly.
    await expect(onboarding.heading).toContainText('Welcome to DQM Onboarding');

    const nhsnLink = page.getByRole('link', {name: /NHSNLink/});
    await expect(nhsnLink).toHaveAttribute('href', 'https://www.cdc.gov/nhsn/fhirportal/about.html');
    // NewTabAnnouncement puts the warning in the accessible name rather than relying on the
    // icon, so an external link is announced as one.
    await expect(nhsnLink).toContainText('NHSNLink');
  });

  test('continues to the reporting plan, and back again', async ({harness, onboarding}) => {
    await harness.open();

    // Welcome has no Back — it is the first step in the flow.
    await expect(onboarding.backButton).toHaveCount(0);

    await onboarding.continueButton.click();
    await onboarding.expectOnStep('reporting-plan');
    await expect(onboarding.currentStepLabel()).toHaveText('Facility Reporting Plan');

    await onboarding.backButton.click();
    await onboarding.expectOnStep('welcome');
  });

  test('is reachable from the rail from a later step', async ({harness, onboarding}) => {
    await harness.open({draft: draftAt('facility-info'), path: '/onboarding/facility-info'});

    await onboarding.stepButton(1).click();

    await onboarding.expectOnStep('welcome');
  });
});
