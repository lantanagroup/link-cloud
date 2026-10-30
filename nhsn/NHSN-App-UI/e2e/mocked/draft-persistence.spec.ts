import {expect, test} from '../support/test';
import {chooseOption} from '../support/fields';
import {draftAt} from '../fixtures/drafts';

/**
 * Characterisation tests for what the draft actually holds after a transition. Both record
 * current behaviour that differs from what the code around it says it does — they are
 * tripwires, so if either starts failing, read the comment before "fixing" the test.
 */
test.describe('draft persistence', () => {
  test('a transition with no edited fields saves nothing at all', async ({harness, onboarding}) => {
    await harness.open();
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('reporting-plan');

    // The transition-persist effect in OnboardingProvider.tsx is guarded by dirtyRef, and
    // Welcome has no fields to dirty, so no save is made — not even of the unlock.
    //
    // src/core/onboarding/walking-skeleton.test.tsx asserts the opposite — "advances to the
    // next step, mirrors it to the URL and persists the draft" — and that unit test is
    // currently **failing** on this branch with "expected null to be truthy", for exactly
    // this reason. So the intended behaviour is persistence, and this spec pins what the app
    // actually does. Fix the provider and both should flip together.
    expect(await harness.storedDraft()).toBeNull();
  });

  test('the stored step pointer lags one transition behind the UI', async ({page, harness, onboarding}) => {
    await harness.open({draft: draftAt('facility-info'), path: '/onboarding/facility-info'});

    await chooseOption(page, 'facilityEhrVendor', 'Epic');
    await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('manual-upload');

    await expect.poll(async () => (await harness.storedDraft())?.facilityInfo?.vendor).toBe('Epic');
    const stored = await harness.storedDraft();

    // advanceTo persists *before* completeGoTo dispatches step/unlock + step/goto, and
    // persistDraft clears dirtyRef on the way, so the post-transition navigation state is
    // never written. The section fields arrive; the pointer does not.
    //
    // Sending the pre-transition draft is deliberate — ApiClient.saveDraft documents that the
    // BFF writes the section for draft.currentStepId, and sending it afterwards would leave
    // that step's values unsaved. The lost *pointer* reads as a side effect rather than an
    // intent: it contradicts the provider's own comment that saving on transition "keeps a
    // later reload agreeing with where back went", since a reload lands the user one step
    // back, on a step they have already completed.
    expect(stored?.currentStepId).toBe('facility-info');
    expect(stored?.unlockedStepIds).not.toContain('manual-upload');
  });
});
