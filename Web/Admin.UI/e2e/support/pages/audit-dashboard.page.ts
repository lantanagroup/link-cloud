import { Locator, Page } from '@playwright/test';

/**
 * /audit — the audit event log.
 *
 * Unlike the rest of the app this screen uses **native `<select>`** rather than mat-select,
 * so its dropdowns are driven with `selectOption()` and there is no page-level overlay to
 * pick options from. Its text filters fire on Enter rather than debouncing on input.
 */
export class AuditDashboardPage {
  readonly table: Locator;
  readonly rows: Locator;
  readonly emptyState: Locator;

  readonly correlationIdFilter: Locator;
  readonly userIdFilter: Locator;
  readonly searchTextFilter: Locator;
  readonly facilityFilter: Locator;
  readonly serviceFilter: Locator;
  readonly actionFilter: Locator;
  readonly clearFiltersButton: Locator;
  readonly refreshButton: Locator;

  constructor(readonly page: Page) {
    this.table = page.locator('table.link-table');
    // The notes panel is rendered as a sibling <tr>, so rows are counted by their data cells
    // rather than by every <tr> in the body.
    this.rows = this.table.locator('tbody tr').filter({ has: page.locator('td.data-col') });
    this.emptyState = page.locator('.no-results-found-container');

    this.correlationIdFilter = page.locator('#reportIdFilter');
    this.userIdFilter = page.locator('#userIdFilter');
    this.searchTextFilter = page.locator('#searchTextFilter');
    this.facilityFilter = page.locator('#facilityFilter');
    this.serviceFilter = page.locator('#serviceFilter');
    this.actionFilter = page.locator('#actionFilter');

    this.clearFiltersButton = page.getByRole('button', { name: 'Clear Filters' });
    this.refreshButton = page.getByRole('button', { name: 'Refresh' });
  }

  async goto(): Promise<void> {
    await this.page.goto('/audit');
    await this.page.getByText('Audit Event Logs').waitFor();
  }

  /** The row whose correlation id cell matches, ignoring the notes row beneath it. */
  rowFor(correlationId: string): Locator {
    return this.rows.filter({ hasText: correlationId });
  }

  /** Text filters only search on Enter — typing alone sends nothing. */
  async submitFilter(field: Locator, value: string): Promise<void> {
    await field.fill(value);
    await field.press('Enter');
  }
}
