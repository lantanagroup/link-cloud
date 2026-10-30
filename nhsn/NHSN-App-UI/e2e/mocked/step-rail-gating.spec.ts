import {expect, test} from '../support/test';
import {draftAt, facilityInfoComplete} from '../fixtures/drafts';

/**
 * `gating.ts` — the single gate every navigation passes through. These assert it through the
 * real browser and the real URL, which `walking-skeleton.test.tsx` cannot: it drives jsdom
 * with `history.pushState`, so it never exercises the dev server's `historyApiFallback` nor
 * a genuine cold load of a deep link.
 */
test.describe('step rail gating', () => {
  test('locks every step after the first on a fresh draft', async ({harness, onboarding}) => {
    await harness.open();

    await expect(onboarding.stepButton(1)).toBeEnabled();
    // Steps 2-13: unlocking records where the user has *been*, so a fresh draft unlocks
    // only welcome even though reporting-plan's own isComplete is unconditionally true.
    for (const index of [2, 5, 8, 13]) {
      await expect(onboarding.stepButton(index)).toBeDisabled();
    }
  });

  test('a deep link to a locked step falls back to the furthest legal one', async ({harness, onboarding}) => {
    await harness.open({path: '/onboarding/complete'});

    // resolveStep rejects the target and the URL is corrected rather than left telling a lie.
    await onboarding.expectOnStep('welcome');
    await expect(onboarding.currentStepLabel()).toHaveText('Welcome');
  });

  test('a deep link to an unlocked step is honoured on a cold load', async ({harness, onboarding}) => {
    await harness.open({
      draft: draftAt('facility-info'),
      path: '/onboarding/facility-info'
    });

    await onboarding.expectOnStep('facility-info');
    await expect(onboarding.heading).toContainText('Facility Information');
  });

  test('falls back when an unlocked step sits behind an incomplete prerequisite', async ({harness, onboarding}) => {
    // `fhir` is unlocked, but `facility-info` is left empty. Both conditions matter in
    // isUnlocked: a stale unlock must not skip a prerequisite the user later cleared.
    await harness.open({
      draft: draftAt('fhir'),
      path: '/onboarding/fhir'
    });

    await onboarding.expectOnStep('facility-info');
  });

  test('reaches the step once that prerequisite is satisfied', async ({harness, onboarding}) => {
    await harness.open({
      draft: draftAt('fhir', {facilityInfo: facilityInfoComplete()}),
      path: '/onboarding/fhir'
    });

    await onboarding.expectOnStep('fhir');
  });
});
