import { Locator, Page } from '@playwright/test';

export class TenantDashboardPage {
  readonly header: Locator;
  readonly createButton: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly emptyState: Locator;
  readonly facilitySearch: Locator;

  constructor(readonly page: Page) {
    this.header = page.getByTestId('tenant-dashboard-header');
    this.createButton = page.getByTestId('facility-create-btn');
    this.table = page.getByTestId('facility-table');
    this.rows = this.table.locator('tr[mat-row]');
    this.emptyState = page.getByTestId('facility-empty-state');
    this.facilitySearch = page.getByTestId('facility-search');
  }

  async goto(): Promise<void> {
    await this.page.goto('/tenant');
    await this.header.waitFor();
  }

  rowFor(facilityId: string): Locator {
    return this.rows.filter({ hasText: facilityId });
  }
}
