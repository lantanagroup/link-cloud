import { Locator, Page, expect } from '@playwright/test';

/** Top-level labels rendered by link-nav-bar.component.ts. */
export const NAV_ITEMS = ['Home', 'Tenants', 'Reports', 'Configuration', 'Logs', 'System'] as const;

export class NavPage {
  readonly toolbarTitle: Locator;
  readonly navBar: Locator;

  constructor(readonly page: Page) {
    this.toolbarTitle = page.locator('mat-toolbar', { hasText: 'Link Administration' });
    this.navBar = page.locator('link-nav-bar');
  }

  async expectShellVisible(): Promise<void> {
    await expect(this.toolbarTitle).toBeVisible();
    await expect(this.navBar).toBeVisible();
    for (const label of NAV_ITEMS) {
      await expect(this.navItem(label)).toBeVisible();
    }
  }

  navItem(label: string): Locator {
    return this.navBar.locator('li.subnav-item > a', { hasText: label });
  }

  dropdownLink(itemLabel: string, childLabel: string): Locator {
    return this.navBar
      .locator('li.subnav-item', { hasText: itemLabel })
      .locator('ul.dropdown-menu a', { hasText: childLabel });
  }

  /** Navigate via a top-level nav link. */
  async clickNav(label: string): Promise<void> {
    await this.navItem(label).click();
  }

  /** Open a dropdown and click a child link (dropdowns open on hover/focus). */
  async clickDropdown(itemLabel: string, childLabel: string): Promise<void> {
    await this.navItem(itemLabel).hover();
    await this.dropdownLink(itemLabel, childLabel).click();
  }
}
