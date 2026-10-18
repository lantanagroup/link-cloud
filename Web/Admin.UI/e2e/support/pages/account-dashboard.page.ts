import { Locator, Page, expect } from '@playwright/test';

/**
 * System → Users (`/account`): the user list and its Account Configuration dialog.
 *
 * The dialog's fields live in a child component rendered inside the Material dialog, so
 * every field locator is scoped to `[role="dialog"]` — the page behind it is not a
 * read-only copy here, but scoping keeps this consistent with the facility pages and
 * survives the list rendering a user with the same name.
 */
export class AccountDashboardPage {
  readonly createUserButton: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly emptyState: Locator;
  readonly showDeletedCheckbox: Locator;
  readonly dialog: Locator;
  readonly saveButton: Locator;
  readonly closeButton: Locator;

  constructor(readonly page: Page) {
    // The template's aria-label is "Create a new user " with a trailing space
    // (account-dashboard.component.html:6). Role-name matching normalises whitespace, so
    // this matches — a CSS [aria-label="..."] selector would not.
    this.createUserButton = page.getByRole('button', { name: 'Create a new user' });
    this.table = page.locator('table[mat-table]');
    this.rows = this.table.locator('tbody tr');
    this.emptyState = page.getByText('No accounts found.');
    this.showDeletedCheckbox = page.getByLabel('Show Deleted Accounts');

    this.dialog = page.getByRole('dialog');
    this.saveButton = this.dialog.getByRole('button', { name: 'Create Account Configuration' });
    this.closeButton = this.dialog.getByRole('button', { name: 'Close' });
  }

  async goto(): Promise<void> {
    await this.page.goto('/account');
  }

  async openCreateDialog(): Promise<void> {
    await this.createUserButton.click();
    await expect(this.dialog).toBeVisible();
  }

  field(label: 'First Name' | 'Last Name' | 'Email'): Locator {
    return this.dialog.getByRole('textbox', { name: label });
  }

  get rolesSelect(): Locator {
    return this.dialog.getByRole('combobox', { name: 'Roles' });
  }

  /**
   * Pick roles from the multi-select. Its options render in a page-level overlay rather
   * than inside the dialog, and because the select is `multiple` it stays open after each
   * choice — Escape is what returns focus to the form and makes the save button reachable.
   */
  async chooseRoles(...names: string[]): Promise<void> {
    await this.rolesSelect.click();
    for (const name of names) {
      await this.page.getByRole('option', { name, exact: true }).click();
    }
    await this.page.keyboard.press('Escape');
  }

  async fillAccount(details: { firstName: string; lastName: string; email: string }): Promise<void> {
    await this.field('First Name').fill(details.firstName);
    await this.field('Last Name').fill(details.lastName);
    await this.field('Email').fill(details.email);
  }

  rowFor(username: string): Locator {
    return this.rows.filter({ hasText: username });
  }

  /** The role badges rendered in a user's Roles cell. */
  roleBadges(username: string): Locator {
    return this.rowFor(username).locator('span.badge');
  }
}
