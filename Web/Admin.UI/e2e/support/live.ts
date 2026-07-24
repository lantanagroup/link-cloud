import { APIRequestContext, Page } from '@playwright/test';

/**
 * API origin for live runs.
 * - Against the compose admin-ui container (UI_E2E_BASE_URL=http://localhost:8066) the
 *   browser calls the BFF cross-origin — default http://localhost:8063/api, overridable
 *   via UI_E2E_API_URL.
 * - Against ng serve (no UI_E2E_BASE_URL) the app uses same-origin /api via the dev proxy.
 */
export function apiBaseUrl(): string {
  if (process.env['UI_E2E_API_URL']) return process.env['UI_E2E_API_URL'];
  const base = process.env['UI_E2E_BASE_URL'];
  // ng serve mode: same-origin /api, proxied to a local BFF on 5218.
  if (base && base.includes(':4200')) return `${base.replace(/\/$/, '')}/api`;
  // Compose mode (the default): the browser calls the BFF cross-origin.
  return 'http://localhost:8063/api';
}

/**
 * Fail fast with a clear message when the BFF behind the live target is not reachable —
 * otherwise a misdirected run (e.g. forgetting UI_E2E_BASE_URL while the stack runs in
 * Docker) fails later with confusing dev-server 404s.
 */
export async function assertBackendReachable(request: APIRequestContext): Promise<void> {
  const api = apiBaseUrl();
  try {
    const response = await request.get(`${api}/info`, { timeout: 10_000 });
    // Anything but a clean 2xx means we are not talking to a healthy BFF — the ng dev
    // server, for instance, answers unproxied /api requests with its own 404 page.
    if (!response.ok()) throw new Error(`GET ${api}/info returned ${response.status()}`);
  } catch (err) {
    throw new Error(
      `Live-tier backend is not reachable at ${api} (${err instanceof Error ? err.message : err}).\n` +
        `- Default target is the docker compose stack: UI on http://localhost:8066, BFF on http://localhost:8063. Is it up?\n` +
        `- To test against ng serve instead, set UI_E2E_BASE_URL=http://localhost:4200 and run a local BFF on 5218 (see proxy.conf.json).`,
    );
  }
}

export interface FailedApiResponse {
  url: string;
  status: number;
}

/** Collect every /api response with status >= 400 for end-of-test assertions. */
export function watchApiFailures(page: Page): FailedApiResponse[] {
  const failures: FailedApiResponse[] = [];
  page.on('response', (response) => {
    if (response.url().includes('/api/') && response.status() >= 400) {
      failures.push({ url: response.url(), status: response.status() });
    }
  });
  return failures;
}
