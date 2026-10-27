import { Locator, Page } from '@playwright/test';

/**
 * /reports — the cross-facility report schedule list. Every filter refetches server-side, so
 * assertions here are usually about the request the UI sent rather than client-side filtering.
 */
export class ReportsDashboardPage {
  readonly header: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly emptyState: Locator;

  readonly facilityFilter: Locator;
  readonly reportIdFilter: Locator;
  readonly statusFilter: Locator;
  readonly frequencyFilter: Locator;
  readonly periodTo: Locator;
  readonly showDeletedCheckbox: Locator;
  readonly clearFiltersButton: Locator;

  constructor(readonly page: Page) {
    this.header = page.getByTestId('reports-dashboard-header');
    this.table = page.getByTestId('reports-table');
    this.rows = this.table.locator('tr[mat-row]');
    this.emptyState = page.getByTestId('reports-empty-state');

    // Material controls carry no testid; their mat-label is the accessible name. Report ID is
    // addressed by role because the "Clear report ID" suffix button shares the same label
    // once the input has text.
    this.facilityFilter = page.getByRole('combobox', { name: 'Facility ID' });
    this.reportIdFilter = page.getByRole('textbox', { name: 'Report ID' });
    this.statusFilter = page.getByRole('combobox', { name: 'Status' });
    this.frequencyFilter = page.getByRole('combobox', { name: 'Frequency' });
    this.periodTo = page.getByRole('textbox', { name: 'To' });
    this.showDeletedCheckbox = page.getByRole('checkbox', { name: /Show deleted/i });
    this.clearFiltersButton = page.getByRole('button', { name: /Clear All Filters/i });
  }

  async goto(): Promise<void> {
    await this.page.goto('/reports');
    await this.header.waitFor();
  }

  /** A sortable column header, which is a button inside the header cell. */
  sortHeader(name: string): Locator {
    return this.table.getByRole('button', { name, exact: true });
  }

  /**
   * The paginator's page-size select cannot be clicked directly: Material overlays it with
   * `.mat-mdc-paginator-touch-target`, which swallows the pointer event and times the click
   * out. Click the touch target instead, then pick from the listbox it opens.
   */
  async setPageSize(size: '5' | '10' | '25' | '50'): Promise<void> {
    await this.page.locator('.mat-mdc-paginator-touch-target').click();
    await this.page.getByRole('option', { name: size, exact: true }).click();
  }
}
