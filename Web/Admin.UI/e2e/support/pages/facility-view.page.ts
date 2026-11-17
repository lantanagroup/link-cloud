import { Locator, Page } from '@playwright/test';

/**
 * /tenant/facility/:facilityId — the read-only facility screen: enrolled reporting,
 * the report-schedule table with its filter bar, and the Locations/Encounters tabs.
 *
 * The header buttons are addressed by test id so a future label edit cannot silently
 * unhook these locators; their accessible names are asserted separately in the spec.
 */
export class FacilityViewPage {
  readonly title: Locator;
  readonly configurationButton: Locator;
  readonly refreshButton: Locator;
  readonly enrolledReportingTable: Locator;
  readonly reportsTable: Locator;
  readonly reportRows: Locator;
  readonly noReportsMessage: Locator;
  readonly reportIdFilter: Locator;
  readonly clearReportIdButton: Locator;
  readonly statusFilter: Locator;
  readonly frequencyFilter: Locator;
  readonly showDeletedCheckbox: Locator;
  readonly clearFiltersButton: Locator;
  readonly nextPageButton: Locator;

  constructor(readonly page: Page) {
    this.title = page.getByTestId('facility-view-title');
    this.configurationButton = page.getByTestId('facility-config-btn');
    this.refreshButton = page.getByTestId('facility-refresh-btn');
    this.enrolledReportingTable = page.getByTestId('enrolled-reporting-table');
    this.reportsTable = page.getByTestId('report-schedules-table');
    this.reportRows = this.reportsTable.locator('tr[mat-row]');
    this.noReportsMessage = this.reportsTable.getByText('No reports found.');
    // Role-scoped: once the field has text, a "Clear report ID" suffix button appears and a
    // bare getByLabel('Report ID') matches both it and the input.
    this.reportIdFilter = page.getByRole('textbox', { name: 'Report ID' });
    this.clearReportIdButton = page.getByRole('button', { name: 'Clear report ID' });
    this.statusFilter = page.getByRole('combobox', { name: 'Status' });
    this.frequencyFilter = page.getByRole('combobox', { name: 'Frequency' });
    this.showDeletedCheckbox = page.getByRole('checkbox', { name: 'Show deleted' });
    this.clearFiltersButton = page.getByRole('button', { name: 'Clear', exact: true });
    this.nextPageButton = page.getByRole('button', { name: 'Next page' });
  }

  async goto(facilityId: string): Promise<void> {
    await this.page.goto(`/tenant/facility/${facilityId}`);
    await this.title.waitFor();
  }

  /** A row of the "Enrolled Reporting" table, e.g. cadenceRow('Monthly'). */
  cadenceRow(cadence: string): Locator {
    return this.enrolledReportingTable.locator('tbody tr').filter({ hasText: cadence });
  }

  /** A row of the report-schedule table, matched by report id. */
  reportRow(reportId: string): Locator {
    return this.reportRows.filter({ hasText: reportId });
  }

  tab(name: string): Locator {
    return this.page.getByRole('tab', { name });
  }
}
