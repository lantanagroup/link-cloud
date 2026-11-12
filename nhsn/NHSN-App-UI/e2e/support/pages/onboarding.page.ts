import {expect, type Locator, type Page} from '@playwright/test';
import type {StepId} from '../../../src/core/onboarding/types';

/**
 * The step machine's chrome — `StepHost.tsx` (rail + panel) and each step's footer.
 *
 * Queries lean on the machine's own class names rather than roles in two places, for the
 * same reasons `walking-skeleton.test.tsx` does: the nav rail and the step rail are both
 * lists, and `PageHeader` renders its title more than once.
 */
export class OnboardingPage {
  readonly stepRail: Locator;
  readonly stepPanel: Locator;
  readonly heading: Locator;
  readonly continueButton: Locator;
  readonly backButton: Locator;
  /** Per-field and summary validation messages. `role="alert"`, so they announce. */
  readonly alerts: Locator;
  /** Transient success/failure feedback, e.g. the FHIR connection test. `role="status"`. */
  readonly statuses: Locator;

  constructor(private readonly page: Page) {
    this.stepRail = page.locator('.nhsn-link__steps');
    this.stepPanel = page.locator('.nhsn-link__step-panel');
    this.heading = page.locator('main h1');
    this.continueButton = page.getByRole('button', {name: 'Continue', exact: true});
    this.backButton = page.getByRole('button', {name: 'Back', exact: true});
    this.alerts = page.locator('main [role="alert"]');
    this.statuses = page.locator('main [role="status"]');
  }

  /** Every rail label, in order — the visible text, not the spelled-out accessible name. */
  stepLabels(): Locator {
    return this.stepRail.locator('.nhsn-link__step-label');
  }

  /** The rail button for a step, by position. Names carry acronym spellings; indexes do not. */
  stepButton(oneBasedIndex: number): Locator {
    return this.stepRail.locator('li').nth(oneBasedIndex - 1).getByRole('button');
  }

  currentStepLabel(): Locator {
    return this.stepRail.locator('.nhsn-link__step--current .nhsn-link__step-label');
  }

  /** Visible alert/status copy, for asserting on the whole set a step is showing at once. */
  async alertTexts(): Promise<string[]> {
    return (await this.alerts.allTextContents()).map((text) => text.trim()).filter(Boolean);
  }

  /**
   * Waits until the URL mirrors `stepId`. `navigation.ts` writes the step to the URL, so
   * this is the machine's own statement about where it is — stronger than the rendered label,
   * which lags behind a lazily-loaded step chunk.
   */
  async expectOnStep(stepId: StepId): Promise<void> {
    await expect(this.page).toHaveURL(new RegExp(`/onboarding/${stepId}$`));
  }
}
