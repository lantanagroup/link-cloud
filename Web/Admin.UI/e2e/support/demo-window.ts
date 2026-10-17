import type { Page } from '@playwright/test';

/**
 * Put the demo window on the best screen available, without anyone having to supply
 * coordinates.
 *
 * Why this exists: monitor coordinates are reported differently by everything that reports
 * them. On this project's own workstation a left-hand monitor's origin reads as -1087,-1440
 * from `[System.Windows.Forms.Screen]::AllScreens`, -1359,-1800 from the same call with DPI
 * awareness switched on, and -1381,-1440 from Chrome. Only the last is the space
 * `--window-position` is interpreted in, so hand-copied coordinates put the window partly
 * off the intended screen and someone ends up dragging it mid-demo.
 *
 * Chrome's own Window Management API reports every screen in exactly that space, so the
 * window can place itself. Nothing machine-specific is stored, and the layout can change
 * between runs without anyone updating a runbook.
 *
 * Degrades quietly: an explicit E2E_WINDOW_POSITION always wins, and a missing API, a
 * refused permission or a single-screen machine leaves the window where Chromium put it.
 * Returns a line describing what it did, for the walkthrough to print.
 */

interface ScreenInfo {
  availLeft: number;
  availTop: number;
  availWidth: number;
  availHeight: number;
  isPrimary: boolean;
  label: string;
}

/** Breathing room between the window and the screen edges. */
const MARGIN = 40;

/** Beyond this the Angular layout stretches rather than filling usefully. */
const MAX_WIDTH = 2400;

export async function placeDemoWindow(page: Page): Promise<string> {
  const explicitPosition = process.env['E2E_WINDOW_POSITION'];
  if (explicitPosition) {
    const size = process.env['E2E_WINDOW_SIZE'] ?? '1600,1000';
    return `window at ${explicitPosition} size ${size} (set explicitly, auto-detect skipped)`;
  }

  const context = page.context();

  // getScreenDetails is only exposed to a secure context, and a page that has not navigated
  // yet is on about:blank, where it does not exist. The walkthrough navigates for itself a
  // moment later; this just gets the page onto the app's origin first.
  if (page.url() === 'about:blank') {
    await page.goto('/');
  }

  // No grantPermissions call: this Playwright build rejects both 'window-management' and the
  // older 'window-placement' as unknown permission names, and Chrome exposes the API to a
  // focused top-level page on a secure origin regardless.
  const screens = await page
    .evaluate(async () => {
      const api = (window as unknown as { getScreenDetails?: () => Promise<{ screens: ScreenLike[] }> })
        .getScreenDetails;
      if (typeof api !== 'function') {
        return null;
      }
      const details = await api.call(window);
      return details.screens.map((s) => ({
        availLeft: s.availLeft,
        availTop: s.availTop,
        availWidth: s.availWidth,
        availHeight: s.availHeight,
        isPrimary: s.isPrimary,
        label: s.label || `${s.availWidth}x${s.availHeight}`,
      }));
    })
    .catch(() => null);

  if (!screens || screens.length === 0) {
    return 'window left where Chromium put it (no screen details available)';
  }
  if (screens.length === 1) {
    return 'window left where Chromium put it (single screen)';
  }

  // The biggest screen that is not the laptop. A presenter's external monitor is almost
  // always both, but prefer "not primary" so the demo does not cover the notes they are
  // reading. If every screen is primary-flagged, fall back to the biggest of them.
  const byArea = (a: ScreenInfo, b: ScreenInfo) =>
    b.availWidth * b.availHeight - a.availWidth * a.availHeight;
  const external = screens.filter((s) => !s.isPrimary).sort(byArea);
  const biggest = external[0] ?? [...screens].sort(byArea)[0];

  // E2E_MONITOR picks a screen by name instead — any part of the label Chrome reports, case
  // insensitive, e.g. 'ultrawide' or 'VX2718'. Names beat coordinates: they are readable,
  // they survive the monitors being rearranged, and a wrong one fails visibly here rather
  // than by putting the window half off a screen.
  const wanted = process.env['E2E_MONITOR']?.trim().toLowerCase();
  const named = wanted ? screens.find((s) => s.label.toLowerCase().includes(wanted)) : undefined;
  if (wanted && !named) {
    const available = screens.map((s) => s.label).join(', ');
    console.warn(`\n  E2E_MONITOR='${wanted}' matched no screen. Available: ${available}\n`);
  }
  const target = named ?? biggest;

  const viewportWidth = Math.min(MAX_WIDTH, target.availWidth - MARGIN * 2);
  // Leave room for the window chrome; the exact height is measured and corrected below.
  const viewportHeight = target.availHeight - MARGIN * 2 - 100;
  await page.setViewportSize({ width: viewportWidth, height: viewportHeight });

  // Centre the *window*, not the viewport: the title bar and tab strip add height that
  // varies by platform and theme, and guessing it is what leaves a window hanging off the
  // bottom of a screen.
  const outer = await page.evaluate(() => ({
    width: window.outerWidth,
    height: window.outerHeight,
  }));
  const left = Math.round(target.availLeft + (target.availWidth - outer.width) / 2);
  const top = Math.round(target.availTop + (target.availHeight - outer.height) / 2);

  const cdp = await context.newCDPSession(page);
  try {
    const { windowId } = await cdp.send('Browser.getWindowForTarget');
    await cdp.send('Browser.setWindowBounds', { windowId, bounds: { left, top } });
  } finally {
    await cdp.detach().catch(() => undefined);
  }

  return `window on ${target.label} (${target.availWidth}x${target.availHeight}) at ${left},${top}, ${outer.width}x${outer.height}`;
}

/** The subset of the Window Management API's Screen this needs. */
interface ScreenLike {
  availLeft: number;
  availTop: number;
  availWidth: number;
  availHeight: number;
  isPrimary: boolean;
  label: string;
}
