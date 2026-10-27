import { Locator, Page } from '@playwright/test';

/**
 * /tenant/facility/:id/edit — the facility configuration screen. Each accordion panel
 * fetches its own configuration on first expand (`(opened)` handlers in the template),
 * so specs must click a panel before its endpoint is called.
 */
export class FacilityEditPage {
  readonly header: Locator;
  readonly backButton: Locator;
  readonly censusPanel: Locator;
  readonly queryDispatchPanel: Locator;
  readonly dataAcquisitionPanel: Locator;
  readonly normalizationPanel: Locator;

  constructor(readonly page: Page) {
    this.header = page.getByTestId('facility-edit-header');
    // aria-label wins over the visible "Back" text for the accessible name.
    this.backButton = page.getByRole('button', { name: 'Navigate to Tenant Dashboard' });
    this.censusPanel = page.locator('#censusPanel');
    this.queryDispatchPanel = page.locator('#queryDispatchPanel');
    this.dataAcquisitionPanel = page.locator('#dataacqPanel');
    this.normalizationPanel = page.locator('#operationsPanel');
  }

  async goto(facilityId: string): Promise<void> {
    await this.page.goto(`/tenant/facility/${facilityId}/edit`);
    await this.header.waitFor();
  }

  /** Expands a panel by clicking its header, which is what triggers its config fetch. */
  async expand(panel: Locator): Promise<void> {
    await panel.locator('mat-expansion-panel-header').click();
  }
}
