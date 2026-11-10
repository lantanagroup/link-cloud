import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, map, Observable, of, shareReplay, throwError } from 'rxjs';

import { AppConfigService } from '../../app-config.service';
import {
  IRubricApiResponse,
  IRubricDefinition,
  IRubricDetail,
  IRubricLifecycleEvent,
  IRubricListQuery,
  IRubricPage,
  IRubricSavePrecondition,
  IRubricSettings,
  IRubricSummary,
  IRubricValidationResult,
  IRubricVersionDetail,
  IRubricVersionSummary,
  RubricDefinitionFormat
} from '../../../interfaces/validation/rubric.interface';

/**
 * A failed call to the rubric registry. The service says what went wrong in its envelope (`message`, and
 * `errors` when a definition is invalid), which the screens show in place rather than as a toast.
 */
export class RubricApiError extends Error {
  constructor(message: string, readonly status: number, readonly errors: string[] = []) {
    super(message);
    this.name = 'RubricApiError';
  }

  /** True when the rubric or version asked for does not exist. */
  get notFound(): boolean {
    return this.status === 404;
  }

  /** True when a save was refused because the version is not in the state the caller said it was. */
  get preconditionFailed(): boolean {
    return this.status === 412;
  }

  /** True when the version's status does not allow what was asked: it is published or retired. */
  get conflict(): boolean {
    return this.status === 409;
  }
}

/**
 * The Validation service's rubric registry. Every endpoint answers with an envelope; the methods here
 * return what is inside it, and fail with a {@link RubricApiError}.
 */
@Injectable({
  providedIn: 'root'
})
export class RubricService {
  private settings$?: Observable<IRubricSettings | null>;

  constructor(private http: HttpClient, private appConfigService: AppConfigService) {
  }

  private get baseUrl(): string {
    return `${this.appConfigService.config?.baseApiUrl}/validation/rubrics`;
  }

  /** One page of rubrics, each with its versions, newest first. */
  listRubrics(query: IRubricListQuery = {}): Observable<IRubricPage<IRubricSummary>> {
    let params = new HttpParams()
      .set('page', query.page ?? 0)
      .set('size', query.size ?? 20);
    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }
    if (query.owner) {
      params = params.set('owner', query.owner);
    }
    if (query.allVersions) {
      params = params.set('allVersions', true);
    }
    if (query.sortBy) {
      params = params.set('sort', `${query.sortBy},${query.sortDirection ?? 'asc'}`);
    }

    return this.http.get<IRubricApiResponse<IRubricPage<IRubricSummary>>>(this.baseUrl, { params }).pipe(
      map(response => response?.data ?? { content: [], page: 0, size: query.size ?? 20, totalElements: 0, totalPages: 0 }),
      catchError(RubricService.toApiError)
    );
  }

  /** A rubric with all its versions, newest first. */
  getRubric(rubricId: string): Observable<IRubricDetail> {
    return this.http.get<IRubricApiResponse<IRubricDetail>>(`${this.baseUrl}/${encodeURIComponent(rubricId)}`).pipe(
      map(response => RubricService.required(response)),
      catchError(RubricService.toApiError)
    );
  }

  /** One version, with its full definition. */
  getVersion(rubricId: string, semver: string): Observable<IRubricVersionDetail> {
    return this.http.get<IRubricApiResponse<IRubricVersionDetail>>(
      `${this.baseUrl}/${encodeURIComponent(rubricId)}/versions/${encodeURIComponent(semver)}`).pipe(
      map(response => RubricService.required(response)),
      catchError(RubricService.toApiError)
    );
  }

  /** A rubric's history, newest first: every time a version was registered, published or retired. */
  getEvents(rubricId: string): Observable<IRubricLifecycleEvent[]> {
    return this.http.get<IRubricApiResponse<IRubricLifecycleEvent[]>>(
      `${this.baseUrl}/${encodeURIComponent(rubricId)}/events`).pipe(
      map(response => response?.data ?? []),
      catchError(RubricService.toApiError)
    );
  }

  /** The owners rubrics have, in order. */
  getOwners(): Observable<string[]> {
    return this.http.get<IRubricApiResponse<string[]>>(`${this.baseUrl}/$owners`).pipe(
      map(response => response?.data ?? []),
      catchError(RubricService.toApiError)
    );
  }

  /**
   * How the service treats rubrics. Asked for once and kept: it is configuration, the same for every
   * screen. Null when it could not be read, so a screen can go on without what it would have added; the
   * next screen asks again.
   */
  getSettings(): Observable<IRubricSettings | null> {
    if (!this.settings$) {
      this.settings$ = this.http.get<IRubricApiResponse<IRubricSettings>>(`${this.baseUrl}/$settings`).pipe(
        map(response => response?.data ?? null),
        catchError(() => {
          this.settings$ = undefined;
          return of(null);
        }),
        shareReplay(1)
      );
    }
    return this.settings$;
  }

  /** A version's definition alone, as the text of a file that registering a version accepts as it is. */
  exportDefinition(rubricId: string, semver: string, format: RubricDefinitionFormat): Observable<string> {
    return this.http.get(
      `${this.baseUrl}/${encodeURIComponent(rubricId)}/versions/${encodeURIComponent(semver)}/definition`,
      {
        headers: { Accept: format === 'yaml' ? 'application/yaml' : 'application/json' },
        responseType: 'text'
      }).pipe(
      catchError(RubricService.toApiError)
    );
  }

  /**
   * Checks a definition without saving it. The text is YAML or JSON as given; `format` says which. The
   * answer is a result even when the definition is not valid: only a request the service could not take
   * at all (too large, say) fails.
   */
  validateText(text: string, format: RubricDefinitionFormat): Observable<IRubricValidationResult> {
    return this.http.post<IRubricApiResponse<IRubricValidationResult>>(`${this.baseUrl}/$validate`, text, {
      headers: { 'Content-Type': format === 'yaml' ? 'application/yaml' : 'application/json' }
    }).pipe(
      map(response => RubricService.validationResult(RubricService.required(response))),
      catchError(RubricService.toApiError)
    );
  }

  /** Checks a definition without saving it. */
  validate(definition: IRubricDefinition): Observable<IRubricValidationResult> {
    return this.validateText(JSON.stringify(definition), 'json');
  }

  /**
   * Saves a definition as a draft of its rubric: a new version, or a replacement for the draft already
   * saved under that version number. Fails with a {@link RubricApiError}: `errors` when the definition is
   * not valid, `preconditionFailed` when the precondition did not hold, `conflict` when that version is
   * published or retired.
   */
  saveDraft(definition: IRubricDefinition, precondition: IRubricSavePrecondition = {}): Observable<IRubricVersionSummary> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (precondition.expectedChecksum) {
      headers['If-Match'] = `"${precondition.expectedChecksum}"`;
    }
    if (precondition.mustNotExist) {
      headers['If-None-Match'] = '*';
    }
    return this.http.post<IRubricApiResponse<IRubricVersionSummary>>(
      `${this.baseUrl}/${encodeURIComponent(definition.id)}/versions`, JSON.stringify(definition), { headers }).pipe(
      map(response => RubricService.required(response)),
      catchError(RubricService.toApiError)
    );
  }

  /** Deletes a draft. A published or retired version cannot be deleted; that fails with `conflict`. */
  deleteDraft(rubricId: string, semver: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/${encodeURIComponent(rubricId)}/versions/${encodeURIComponent(semver)}`).pipe(
      map(() => undefined),
      catchError(RubricService.toApiError)
    );
  }

  private static validationResult(result: IRubricValidationResult): IRubricValidationResult {
    return { ...result, valid: result.valid === true, errors: result.errors ?? [] };
  }

  private static toApiError(error: unknown): Observable<never> {
    if (error instanceof RubricApiError) {
      return throwError(() => error);
    }
    if (error instanceof HttpErrorResponse) {
      const envelope = RubricService.envelopeOf(error.error);
      const message = typeof envelope?.message === 'string' && envelope.message
        ? envelope.message
        : error.status === 0
          ? 'The Validation service could not be reached.'
          : `The Validation service returned an error (${error.status}).`;
      const errors = Array.isArray(envelope?.errors) ? envelope.errors.filter(e => typeof e === 'string') : [];
      return throwError(() => new RubricApiError(message, error.status, errors));
    }
    return throwError(() => new RubricApiError(error instanceof Error ? error.message : 'An unknown error occurred.', 0));
  }

  // A request for text gets its error body as text too, so the envelope has to be read out of it.
  private static envelopeOf(body: unknown): Partial<IRubricApiResponse<unknown>> | null {
    if (typeof body === 'string') {
      try {
        return JSON.parse(body);
      } catch {
        return null;
      }
    }
    return body as Partial<IRubricApiResponse<unknown>> | null;
  }

  private static required<T>(response: IRubricApiResponse<T> | null): T {
    if (response?.data === undefined || response.data === null) {
      throw new RubricApiError('The Validation service returned no data.', response?.status ?? 0);
    }
    return response.data;
  }
}
