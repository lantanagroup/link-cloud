import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AppConfigService } from '../../app-config.service';
import { RubricApiError, RubricService } from './rubric.service';

const BASE = '/api/validation/rubrics';

function envelope<T>(data: T) {
  return { status: 200, message: 'OK', timestamp: '2026-09-08T10:15:30.184Z', data };
}

describe('RubricService', () => {
  let service: RubricService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AppConfigService, useValue: { config: { baseApiUrl: '/api' } } }
      ]
    });
    service = TestBed.inject(RubricService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('listRubrics', () => {
    it('asks for the first page of 20 when given nothing, with no status and no sort', () => {
      service.listRubrics().subscribe();

      const request = http.expectOne(r => r.url === BASE);
      expect(request.request.params.get('page')).toBe('0');
      expect(request.request.params.get('size')).toBe('20');
      expect(request.request.params.has('status')).toBeFalse();
      expect(request.request.params.has('sort')).toBeFalse();
      request.flush(envelope({ content: [], page: 0, size: 20, totalElements: 0, totalPages: 0 }));
    });

    it('passes the status, page, size and sort it is given', () => {
      service.listRubrics({ status: 'DRAFT', page: 2, size: 50, sortBy: 'updatedAt', sortDirection: 'desc' }).subscribe();

      const request = http.expectOne(r => r.url === BASE);
      expect(request.request.params.get('status')).toBe('DRAFT');
      expect(request.request.params.get('page')).toBe('2');
      expect(request.request.params.get('size')).toBe('50');
      expect(request.request.params.get('sort')).toBe('updatedAt,desc');
      request.flush(envelope({ content: [], page: 2, size: 50, totalElements: 0, totalPages: 0 }));
    });

    it('passes a search (trimmed), an owner and allVersions, and leaves each out when it is not asked for', () => {
      service.listRubrics({ search: '  core ', owner: 'lcg', allVersions: true }).subscribe();

      const request = http.expectOne(r => r.url === BASE);
      expect(request.request.params.get('search')).toBe('core');
      expect(request.request.params.get('owner')).toBe('lcg');
      expect(request.request.params.get('allVersions')).toBe('true');
      request.flush(envelope({ content: [], page: 0, size: 20, totalElements: 0, totalPages: 0 }));

      service.listRubrics({ search: '   ', owner: '', allVersions: false }).subscribe();

      const plain = http.expectOne(r => r.url === BASE);
      expect(plain.request.params.has('search')).toBeFalse();
      expect(plain.request.params.has('owner')).toBeFalse();
      expect(plain.request.params.has('allVersions')).toBeFalse();
      plain.flush(envelope({ content: [], page: 0, size: 20, totalElements: 0, totalPages: 0 }));
    });

    it('returns the page inside the envelope', () => {
      const page = { content: [{ rubricId: 'a' }], page: 0, size: 20, totalElements: 1, totalPages: 1 };
      let received: unknown;

      service.listRubrics().subscribe(result => received = result);
      http.expectOne(r => r.url === BASE).flush(envelope(page));

      expect(received).toEqual(page);
    });

    it('returns an empty page when the service answers with no body', () => {
      let received: { content: unknown[]; totalElements: number } | undefined;

      service.listRubrics({ size: 10 }).subscribe(result => received = result);
      http.expectOne(r => r.url === BASE).flush(null);

      expect(received?.content).toEqual([]);
      expect(received?.totalElements).toBe(0);
    });
  });

  it('getRubric encodes the id and returns the rubric inside the envelope', () => {
    let received: unknown;

    service.getRubric('a b/c').subscribe(result => received = result);
    http.expectOne(`${BASE}/a%20b%2Fc`).flush(envelope({ rubricId: 'a b/c' }));

    expect(received).toEqual({ rubricId: 'a b/c' });
  });

  it('getVersion asks for the version under its rubric', () => {
    let received: unknown;

    service.getVersion('piqi.core', '1.2.0').subscribe(result => received = result);
    http.expectOne(`${BASE}/piqi.core/versions/1.2.0`).flush(envelope({ semver: '1.2.0' }));

    expect(received).toEqual({ semver: '1.2.0' });
  });

  it('getEvents returns the events, or none when the envelope has no data', () => {
    let first: unknown;
    let second: unknown;

    service.getEvents('r').subscribe(result => first = result);
    http.expectOne(`${BASE}/r/events`).flush(envelope([{ eventId: 'e' }]));
    service.getEvents('r').subscribe(result => second = result);
    http.expectOne(`${BASE}/r/events`).flush({ status: 200, message: 'OK', timestamp: 't' });

    expect(first).toEqual([{ eventId: 'e' }]);
    expect(second).toEqual([]);
  });

  it('getOwners returns the owners', () => {
    let received: unknown;

    service.getOwners().subscribe(result => received = result);
    http.expectOne(`${BASE}/$owners`).flush(envelope(['lantana', 'lcg']));

    expect(received).toEqual(['lantana', 'lcg']);
  });

  describe('getSettings', () => {
    it('asks once, however many ask', () => {
      const received: unknown[] = [];

      service.getSettings().subscribe(result => received.push(result));
      service.getSettings().subscribe(result => received.push(result));
      http.expectOne(`${BASE}/$settings`).flush(envelope({ pipelineRubricId: 'piqi.core' }));
      service.getSettings().subscribe(result => received.push(result));

      expect(received).toEqual([
        { pipelineRubricId: 'piqi.core' }, { pipelineRubricId: 'piqi.core' }, { pipelineRubricId: 'piqi.core' }]);
    });

    it('gives null instead of failing when the settings cannot be read, and asks again next time', () => {
      let first: unknown = 'unset';
      let second: unknown = 'unset';

      service.getSettings().subscribe(result => first = result);
      http.expectOne(`${BASE}/$settings`).flush('nope', { status: 500, statusText: 'Server Error' });
      service.getSettings().subscribe(result => second = result);
      http.expectOne(`${BASE}/$settings`).flush(envelope({ pipelineRubricId: 'piqi.core' }));

      expect(first).toBeNull();
      expect(second).toEqual({ pipelineRubricId: 'piqi.core' });
    });
  });

  describe('exportDefinition', () => {
    const URL = `${BASE}/piqi.core/versions/1.2.0/definition`;

    it('asks for YAML with the Accept header and returns the text as it came', () => {
      let received: unknown;

      service.exportDefinition('piqi.core', '1.2.0', 'yaml').subscribe(result => received = result);
      const request = http.expectOne(URL);
      expect(request.request.headers.get('Accept')).toBe('application/yaml');
      expect(request.request.responseType).toBe('text');
      request.flush('id: piqi.core\nsemver: 1.2.0\n');

      expect(received).toBe('id: piqi.core\nsemver: 1.2.0\n');
    });

    it('asks for JSON otherwise, and does not parse it', () => {
      let received: unknown;

      service.exportDefinition('piqi.core', '1.2.0', 'json').subscribe(result => received = result);
      const request = http.expectOne(URL);
      expect(request.request.headers.get('Accept')).toBe('application/json');
      request.flush('{\n  "id" : "piqi.core"\n}');

      expect(received).toBe('{\n  "id" : "piqi.core"\n}');
    });

    it('reads the service\'s message out of an error that arrives as text', () => {
      let caught: RubricApiError | undefined;

      service.exportDefinition('piqi.core', '1.2.0', 'yaml').subscribe({ error: error => caught = error });
      http.expectOne(URL).flush(
        JSON.stringify({ status: 404, message: 'Rubric version not found', timestamp: 't' }),
        { status: 404, statusText: 'Not Found' });

      expect(caught?.message).toBe('Rubric version not found');
      expect(caught?.notFound).toBeTrue();
    });
  });

  describe('a failed call', () => {
    function failure(run: () => void): RubricApiError {
      let caught: RubricApiError | undefined;
      service.getRubric('nope').subscribe({ error: error => caught = error });
      run();
      expect(caught).toBeInstanceOf(RubricApiError);
      return caught as RubricApiError;
    }

    it('carries the message and status the service gave', () => {
      const error = failure(() => http.expectOne(`${BASE}/nope`).flush(
        { status: 404, message: 'Rubric not found: nope', timestamp: 't' }, { status: 404, statusText: 'Not Found' }));

      expect(error.message).toBe('Rubric not found: nope');
      expect(error.status).toBe(404);
      expect(error.notFound).toBeTrue();
      expect(error.errors).toEqual([]);
    });

    it('carries the list of errors when the service gave one', () => {
      const error = failure(() => http.expectOne(`${BASE}/nope`).flush(
        { status: 400, message: 'Invalid rubric definition', errors: ['semver: must not be blank'] },
        { status: 400, statusText: 'Bad Request' }));

      expect(error.errors).toEqual(['semver: must not be blank']);
      expect(error.notFound).toBeFalse();
    });

    it('says the status when the answer is not the service\'s envelope', () => {
      const error = failure(() => http.expectOne(`${BASE}/nope`).flush('<html>', { status: 502, statusText: 'Bad Gateway' }));

      expect(error.message).toBe('The Validation service returned an error (502).');
      expect(error.status).toBe(502);
    });

    it('says the service could not be reached when there is no answer at all', () => {
      const error = failure(() => http.expectOne(`${BASE}/nope`).error(new ProgressEvent('error')));

      expect(error.message).toBe('The Validation service could not be reached.');
      expect(error.status).toBe(0);
    });

    it('fails when a successful answer has no data', () => {
      const error = failure(() => http.expectOne(`${BASE}/nope`).flush({ status: 200, message: 'OK', timestamp: 't' }));

      expect(error.message).toBe('The Validation service returned no data.');
    });
  });
});
