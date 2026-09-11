/**
 * The fetch wrapper the API adapters share.
 *
 * Owns: JSON handling, timeouts, typed errors, ETag plumbing and the
 * 202-plus-poll pattern. Deliberately does NOT own: authentication, retries of
 * business operations, or any knowledge of onboarding. Those belong above it.
 */

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  extensions?: Record<string, unknown>;
}

export class HttpError extends Error {
  constructor(
    readonly status: number,
    readonly url: string,
    readonly problem?: ProblemDetails,
    readonly correlationId?: string
  ) {
    super(problem?.detail || problem?.title || `Request failed (${status}).`);
    this.name = 'HttpError';
  }

  /**
   * The downstream trace id, when the BFF surfaced a Link failure as a 502.
   * This is the field worth quoting when a facility reports a problem and the
   * logs are in Loki.
   */
  get downstreamTraceId(): string | undefined {
    const value = this.problem?.extensions?.['downstreamTraceId'];
    return typeof value === 'string' ? value : undefined;
  }
}

export class TimeoutError extends Error {
  constructor(readonly url: string, readonly timeoutMs: number) {
    super(`Request timed out after ${timeoutMs}ms.`);
    this.name = 'TimeoutError';
  }
}

export interface HttpResult<T> {
  data: T;
  etag?: string;
  /** Present on 202 responses. */
  location?: string;
  status: number;
}

export interface RequestOptions {
  method?: string;
  body?: unknown;
  headers?: Record<string, string>;
  timeoutMs?: number;
  signal?: AbortSignal;
  /** Return the raw body rather than parsing JSON. */
  responseType?: 'json' | 'blob';
}

const DEFAULT_TIMEOUT_MS = 30_000;

export class HttpClient {
  constructor(
    private readonly baseUrl: string,
    private readonly defaultTimeoutMs: number = DEFAULT_TIMEOUT_MS
  ) {}

  async request<T>(path: string, options: RequestOptions = {}): Promise<HttpResult<T>> {
    const url = `${this.baseUrl}${path}`;
    const timeoutMs = options.timeoutMs ?? this.defaultTimeoutMs;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);

    // Caller cancellation (unmount) and our timeout both feed one signal.
    const onExternalAbort = () => controller.abort();
    options.signal?.addEventListener('abort', onExternalAbort);

    const headers: Record<string, string> = {
      Accept: options.responseType === 'blob' ? '*/*' : 'application/json',
      ...options.headers
    };

    const isFormData = options.body instanceof FormData;
    if (options.body !== undefined && !isFormData) {
      headers['Content-Type'] = 'application/json';
    }

    let response: Response;
    try {
      response = await fetch(url, {
        method: options.method ?? 'GET',
        headers,
        // NO Authorization header, ever. The NHSN gateway injects the JWT in
        // transit; the component never touches the token. This absence is an
        // ADR requirement, not an oversight.
        credentials: 'include',
        body: serializeBody(options.body),
        signal: controller.signal
      });
    } catch (error) {
      if (controller.signal.aborted && !options.signal?.aborted) {
        throw new TimeoutError(url, timeoutMs);
      }
      throw error;
    } finally {
      clearTimeout(timer);
      options.signal?.removeEventListener('abort', onExternalAbort);
    }

    const correlationId = response.headers.get('X-Correlation-Id') ?? undefined;

    if (!response.ok) {
      const problem = await readProblem(response);
      throw new HttpError(response.status, url, problem, correlationId);
    }

    return {
      data: await readBody<T>(response, options.responseType ?? 'json'),
      etag: response.headers.get('ETag') ?? undefined,
      location: response.headers.get('Location') ?? undefined,
      status: response.status
    };
  }

  get<T>(path: string, options?: RequestOptions) {
    return this.request<T>(path, { ...options, method: 'GET' });
  }

  post<T>(path: string, body?: unknown, options?: RequestOptions) {
    return this.request<T>(path, { ...options, method: 'POST', body });
  }

  put<T>(path: string, body?: unknown, options?: RequestOptions) {
    return this.request<T>(path, { ...options, method: 'PUT', body });
  }
}

function serializeBody(body: unknown): BodyInit | undefined {
  if (body === undefined) {
    return undefined;
  }
  if (body instanceof FormData || body instanceof Blob) {
    return body;
  }
  return JSON.stringify(body);
}

async function readBody<T>(response: Response, responseType: 'json' | 'blob'): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }
  if (responseType === 'blob') {
    return (await response.blob()) as T;
  }
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  // Link services are not uniform here — Tenant returns plain text on a
  // duplicate create while Data Acquisition returns ProblemDetails. The BFF
  // normalizes, but never assume a parseable body.
  try {
    const text = await response.text();
    if (!text) {
      return undefined;
    }
    const parsed = JSON.parse(text) as ProblemDetails;
    return typeof parsed === 'object' && parsed !== null ? parsed : { detail: text };
  } catch {
    return undefined;
  }
}

// ---------------------------------------------------------------- long-running operations

export type OperationState = 'running' | 'succeeded' | 'failed';

/**
 * Handle for a request/poll operation. Report generation, census pulls and
 * sFTP listing are slow: the BFF answers 202 with a Location and the UI polls.
 *
 * Steps await `result()`; they never write polling loops, so the backoff lives
 * here in one place.
 */
export interface Operation<T> {
  state: OperationState;
  result(): Promise<T>;
  cancel(): void;
}

export interface PollOptions {
  intervalMs?: number;
  maxIntervalMs?: number;
  timeoutMs?: number;
  /** Decides whether a polled payload is terminal. */
  isDone: (value: unknown) => boolean;
}

const POLL_DEFAULTS = {
  intervalMs: 2_000,
  maxIntervalMs: 15_000,
  timeoutMs: 10 * 60_000
};

/**
 * Turns a 202 + Location response into an Operation. If the response was not a
 * 202, the value is already terminal and resolves immediately — so callers do
 * not branch on whether the backend happened to answer synchronously.
 */
export function pollOperation<T>(
  http: HttpClient,
  initial: HttpResult<T>,
  options: PollOptions
): Operation<T> {
  const controller = new AbortController();
  const operation: Operation<T> = {
    state: 'running',
    cancel: () => controller.abort(),
    result: () => promise
  };

  const promise = (async () => {
    if (initial.status !== 202 || !initial.location) {
      operation.state = 'succeeded';
      return initial.data;
    }

    const location = initial.location;
    const startedAt = Date.now();
    const timeoutMs = options.timeoutMs ?? POLL_DEFAULTS.timeoutMs;
    let interval = options.intervalMs ?? POLL_DEFAULTS.intervalMs;
    const maxInterval = options.maxIntervalMs ?? POLL_DEFAULTS.maxIntervalMs;

    try {
      for (;;) {
        if (Date.now() - startedAt > timeoutMs) {
          throw new TimeoutError(location, timeoutMs);
        }
        await delay(interval, controller.signal);
        const next = await http.get<T>(location, { signal: controller.signal });
        if (options.isDone(next.data)) {
          operation.state = 'succeeded';
          return next.data;
        }
        interval = Math.min(interval * 1.5, maxInterval);
      }
    } catch (error) {
      operation.state = 'failed';
      throw error;
    }
  })();

  return operation;
}

function delay(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(new DOMException('Aborted', 'AbortError'));
      return;
    }
    const timer = setTimeout(() => {
      signal.removeEventListener('abort', onAbort);
      resolve();
    }, ms);
    function onAbort() {
      clearTimeout(timer);
      reject(new DOMException('Aborted', 'AbortError'));
    }
    signal.addEventListener('abort', onAbort, { once: true });
  });
}
