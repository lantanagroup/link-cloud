import {expect, type Locator, type Page} from '@playwright/test';

/**
 * Selector contract for `src/core/fields` — the design-system adapter every step renders
 * through. All of it is `@nhsn/nhsn-react-core` (Kendo) rather than native controls, so the
 * traps here apply everywhere, not just where one spec happened to meet them.
 */

/**
 * A `Select` is a Kendo `FormDropDownList`, **not** a `<select>`: `selectOption()` does
 * nothing on it. The host element carries the field id and `role="combobox"`; options render
 * into a `.k-animation-container` appended to the body — page level, not inside the field —
 * as `role="option"` with ids `option-<fieldId>-guid-<n>`.
 *
 * `Select.tsx` prepends the placeholder as a real option (`PLACEHOLDER_KEY`), so entry 0 of
 * that list is "Select a …", not the first value.
 */
export async function chooseOption(page: Page, fieldId: string, option: string | RegExp): Promise<void> {
  await page.locator(`#${fieldId}`).click();
  const choice = page.getByRole('option', {
    name: option,
    // A string names one option exactly; a RegExp is already as loose as it means to be.
    exact: typeof option === 'string'
  });
  await choice.click();
  // The popup animates out; waiting for it keeps the next click off a closing overlay.
  await expect(choice).toBeHidden();
}

/**
 * The text a `Select` currently displays — its placeholder when nothing is chosen.
 *
 * The host element's own text, not a `.k-input-value-text` child: this Kendo build renders the
 * chosen label in an unclassed span, and the only other text inside the combobox belongs to the
 * icon-only "select" button, which contributes none.
 */
export function selectedOptionText(page: Page, fieldId: string): Locator {
  return page.locator(`#${fieldId}`);
}

/**
 * Two things in `core/fields/layout.tsx` rewrite accessible names, which is what
 * `getByRole(..., {name})` matches. Visible text and validation copy are untouched.
 *
 * 1. `AcronymText` renders `<span aria-hidden>EHR</span><span class="visually-hidden">E H
 *    R</span>` for each of HSLOC / POI / EHR (`ACRONYM_SPELLINGS`), so the name carries the
 *    spelled form. `getByRole('combobox', {name: 'EHR Vendor'})` matches nothing — the name
 *    is `E H R Vendor`.
 *
 *    Do not compute this with a string replace. Where the acronym abuts punctuation the
 *    accessible name also picks up element-boundary spacing, so step 8 is
 *    `8 Location Identification ( H S L O C )` — spaces *inside* the parentheses, which the
 *    app's own `acronymLabel()` does not produce. Match such names with a RegExp on the
 *    stable part instead of spelling the whole thing out.
 *
 * 2. `HeadingPause` appends a visually-hidden `.` to step headings, so every `<h1>` reads
 *    `Facility Information .`. Assert headings with `toContainText`, never exact text.
 */
export const ACRONYM_SPELLED = {
  ehrVendor: 'E H R Vendor'
} as const;
