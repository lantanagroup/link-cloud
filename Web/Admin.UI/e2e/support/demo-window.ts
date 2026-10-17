import type { BrowserContext, Page } from '@playwright/test';

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

  // Grant window management up front, through CDP. Without this Chrome puts up an "Allow
  // this site to manage windows?" prompt: the presenter has to click it, and the window sits
  // on the laptop until they do, so the demo opens on the wrong screen and then jumps.
  // Playwright's own grantPermissions does not accept this one — it rejects both
  // 'window-management' and the older 'window-placement' as unknown names — so go to the
  // browser session directly. Chrome renamed the CDP permission too, hence both spellings.
  const browser = context.browser();
  if (browser) {
    try {
      // The browserContextId matters: Playwright runs its pages in a non-default browser
      // context, and a grant sent without one lands on the default context, where nothing
      // of ours is running. The prompt then appears exactly as if nothing had been granted.
      const pageSession = await context.newCDPSession(page);
      const { targetInfo } = await pageSession.send('Target.getTargetInfo');
      await pageSession.detach().catch(() => undefined);

      const browserSession = await browser.newBrowserCDPSession();
      await browserSession.send('Browser.grantPermissions', {
        origin: new URL(page.url()).origin,
        browserContextId: targetInfo.browserContextId,
        permissions: ['windowManagement'],
      });
    } catch {
      // Older Chrome called this windowPlacement and newer protocol typings no longer list
      // it. Not worth chasing: without the grant the presenter gets the prompt they would
      // have got anyway, and declining it just leaves the window where Chromium put it.
    }
  }

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

  // The window launched off the virtual desktop, so there is no "leave it where it is"
  // option any more: if detection failed, put it somewhere visible on the primary screen
  // or the demo runs against a window nobody can see.
  if (!screens || screens.length === 0) {
    await moveWindow(context, page, { left: 60, top: 60 });
    return 'window at 60,60 on the primary screen (no screen details available)';
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

  // Size and position in one go. With no viewport emulation the page simply fills whatever
  // window it is given, so the window is the only thing to set.
  const width = Math.min(MAX_WIDTH, target.availWidth - MARGIN * 2);
  const height = target.availHeight - MARGIN * 2;
  const left = Math.round(target.availLeft + (target.availWidth - width) / 2);
  const top = Math.round(target.availTop + (target.availHeight - height) / 2);

  await moveWindow(context, page, { left, top, width, height });

  // Ask the browser which screen it actually ended up on, rather than trusting the maths.
  // currentScreen is the one the window is displayed on, so this catches a coordinate space
  // mismatch — the failure mode that put the window half onto the next monitor before.
  const landedOn = await page
    .evaluate(async () => {
      const api = (window as unknown as { getScreenDetails?: () => Promise<{ currentScreen: { label: string } }> })
        .getScreenDetails;
      return typeof api === 'function' ? (await api.call(window)).currentScreen.label : null;
    })
    .catch(() => null);

  const placement = `${width}x${height} at ${left},${top}`;
  if (landedOn && landedOn !== target.label) {
    return `window asked for ${target.label} but landed on ${landedOn} — ${placement}`;
  }
  return `window on ${target.label} (${target.availWidth}x${target.availHeight}), ${placement}`;
}

/** Move the real OS window. Playwright has no API for this; CDP does. */
async function moveWindow(
  context: BrowserContext,
  page: Page,
  bounds: { left: number; top: number; width?: number; height?: number },
): Promise<void> {
  const cdp = await context.newCDPSession(page);
  try {
    const { windowId } = await cdp.send('Browser.getWindowForTarget');
    await cdp.send('Browser.setWindowBounds', { windowId, bounds });
  } finally {
    await cdp.detach().catch(() => undefined);
  }
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
