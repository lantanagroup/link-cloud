import {expect, type Locator, type Page} from '@playwright/test';
import type {FacilityDraft} from '../../../src/core/onboarding/types';
import {MOCK_DRAFT_KEY} from '../../fixtures/drafts';
import {chooseOption} from '../fields';

/**
 * `src/shell/facilities/FacilityHarness.tsx` — the standalone shell around the embedded
 * component. Throwaway in production terms, unavoidable in test terms: it is the only thing
 * that mounts the wizard outside the CDC NHSN App.
 *
 * Two of its behaviours shape every spec in this suite:
 *
 * 1. **Mode defaults to `bff`, not mock** (`useState<HarnessMode>('bff')`), despite what the
 *    README says. With no saved profile the harness builds no client at all, so a fresh
 *    context lands on the "Select or create a test profile" empty state — not the wizard.
 * 2. **Mode is component state, not storage.** Nothing persists it, so *every* navigation —
 *    `goto`, `reload`, back/forward — drops back to BFF mode and unmounts the wizard. That
 *    is why `open()` re-enters mock mode after each navigation, and why no spec in the
 *    mocked tier asserts on reload behaviour: in this harness a reload cannot preserve the
 *    wizard at all. (The draft itself does survive — it is in localStorage.)
 */
export class HarnessPage {
  readonly profileGate: Locator;
  readonly stepRail: Locator;
  readonly stepPanel: Locator;
  readonly facilityName: Locator;
  readonly facilityId: Locator;

  constructor(private readonly page: Page) {
    this.profileGate = page.locator('.shell__empty');
    this.stepRail = page.locator('.nhsn-link__steps');
    this.stepPanel = page.locator('.nhsn-link__step-panel');
    this.facilityName = page.locator('.shell__content .nhsn-link__step-nav-facility-name');
    this.facilityId = page.locator('.shell__content .nhsn-link__step-nav-facility-id');
  }

  /**
   * Seed a draft (optional), land on `path`, and switch the harness into mock mode.
   *
   * The draft is written with `addInitScript` so it is in storage before the app's first
   * read — `MockApiClient.getDraft()` runs on mount, and a draft written after that is a
   * draft the wizard never sees.
   */
  async open(options: {draft?: FacilityDraft; path?: string} = {}): Promise<void> {
    if (options.draft) {
      await this.seedDraft(options.draft);
    }
    await this.page.goto(options.path ?? '/');
    await this.enterMockMode();
  }

  /** Writes the mock draft for every subsequent page load in this context. */
  async seedDraft(draft: FacilityDraft): Promise<void> {
    await this.page.addInitScript(
      ([key, value]) => window.localStorage.setItem(key, value),
      [MOCK_DRAFT_KEY, JSON.stringify(draft)] as const
    );
  }

  /** Reads back what the app has actually persisted, for assertions about saving. */
  async storedDraft(): Promise<FacilityDraft | null> {
    return this.page.evaluate((key) => {
      const raw = window.localStorage.getItem(key);
      return raw ? (JSON.parse(raw) as FacilityDraft) : null;
    }, MOCK_DRAFT_KEY);
  }

  /** The sidebar starts collapsed on every load, so the mode control needs revealing first. */
  async showControls(): Promise<void> {
    const toggle = this.page.getByRole('button', {name: 'Show harness controls'});
    if (await toggle.isVisible()) {
      await toggle.click();
    }
  }

  async enterMockMode(): Promise<void> {
    await this.showControls();
    // Label is "Backend" (shell.modeLabel), not "Harness Mode" — that is the card heading.
    await chooseOption(this.page, 'harnessMode', 'Mock (offline)');
    // Mock mode needs no profile: MockApiClient takes the facility id as an optional arg.
    await expect(this.stepRail).toBeVisible();

    // Wait for the *panel*, not just the rail. StepHost renders the loading indicator in
    // place of the panel until getDraft() resolves, while OnboardingStepsNav renders
    // throughout — against `createEmptyDraft()`, whose current step is `welcome`. Acting on
    // the rail in that window silently does nothing: goTo() returns early when the target is
    // already the current step, so a click on "Welcome" is swallowed and the seeded draft
    // then moves the machine elsewhere. The panel is the machine's own ready signal.
    await expect(this.stepPanel).toBeVisible();
  }
}
