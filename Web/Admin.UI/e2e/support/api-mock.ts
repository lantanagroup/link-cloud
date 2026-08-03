import { Page, Route } from '@playwright/test';

export type ApiHandler =
  | object // fulfilled as JSON 200
  | { status: number; json?: object; body?: string; contentType?: string }
  | ((route: Route) => Promise<void> | void);

export interface RecordedCall {
  method: string;
  url: string;
  postData: string | null;
}

/**
 * Handler map keyed by "METHOD /path" (path is matched against the URL pathname).
 * A trailing "*" in the key path acts as a prefix wildcard, e.g. "GET /api/facility/*".
 *
 * Anything under /api that no handler matches is fulfilled with 404 and recorded in
 * `unmatched` — specs assert it stays empty, which is the tripwire that flags new
 * endpoints being called without a fixture.
 */
export class ApiMock {
  readonly calls: RecordedCall[] = [];
  readonly unmatched: RecordedCall[] = [];
  private handlers = new Map<string, ApiHandler>();

  constructor(private page: Page) {}

  async install(): Promise<void> {
    await this.page.route('**/api/**', async (route) => {
      const request = route.request();
      const method = request.method().toUpperCase();
      const pathname = new URL(request.url()).pathname;
      const record: RecordedCall = { method, url: request.url(), postData: request.postData() };
      this.calls.push(record);

      const handler = this.resolve(method, pathname);
      if (!handler) {
        this.unmatched.push(record);
        await route.fulfill({ status: 404, json: { error: `No e2e fixture for ${method} ${pathname}` } });
        return;
      }
      if (typeof handler === 'function') {
        await handler(route);
      } else if ('status' in handler && typeof handler.status === 'number') {
        const { status, json, body, contentType } = handler as { status: number; json?: object; body?: string; contentType?: string };
        await route.fulfill({ status, json, body, contentType });
      } else {
        await route.fulfill({ status: 200, json: handler });
      }
    });
  }

  /** Register (or replace) a handler, e.g. mock('GET /api/facility', pagedFacilities). */
  mock(key: string, handler: ApiHandler): this {
    this.handlers.set(key.replace(/\s+/g, ' ').trim(), handler);
    return this;
  }

  /** Calls whose pathname matches the given prefix, for request-shape assertions. */
  callsTo(method: string, pathPrefix: string): RecordedCall[] {
    return this.calls.filter(
      (c) => c.method === method.toUpperCase() && new URL(c.url).pathname.startsWith(pathPrefix),
    );
  }

  private resolve(method: string, pathname: string): ApiHandler | undefined {
    const exact = this.handlers.get(`${method} ${pathname}`);
    if (exact) return exact;
    for (const [key, handler] of this.handlers) {
      const [m, p] = key.split(' ');
      if (m !== method || !p) continue;
      if (p.endsWith('*') ? pathname.startsWith(p.slice(0, -1)) : p === pathname) return handler;
    }
    return undefined;
  }
}
