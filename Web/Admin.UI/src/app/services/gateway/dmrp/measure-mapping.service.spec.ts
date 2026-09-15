import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { throwError } from 'rxjs';

import { MeasureMappingService } from './measure-mapping.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { AppConfigService } from '../../app-config.service';
import { Frequency, IMeasureMapping, IPagedMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

const BASE = 'http://link.test/api';

describe('MeasureMappingService', () => {
  let service: MeasureMappingService;
  let http: HttpTestingController;
  let errorHandler: jasmine.SpyObj<ErrorHandlingService>;

  beforeEach(() => {
    errorHandler = jasmine.createSpyObj<ErrorHandlingService>('ErrorHandlingService', ['handleError']);
    errorHandler.handleError.and.callFake((err: any) => throwError(() => err));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ErrorHandlingService, useValue: errorHandler },
        { provide: AppConfigService, useValue: { config: { baseApiUrl: BASE } } }
      ]
    });

    service = TestBed.inject(MeasureMappingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function failRequest(call: () => void, method: string, url: string): void {
    call();
    http.expectOne(r => r.method === method && r.url === url)
      .flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
  }

  function toastrShown(): boolean {
    return errorHandler.handleError.calls.mostRecent().args[1] !== false;
  }

  it('searches with the API 1-based page number and reverts the response back to 0-based', () => {
    let result: IPagedMeasureMapping | null = null;
    service.searchMeasureMappings('', '', null, 'Measure', 0, 10, 0).subscribe(r => (result = r));

    const req = http.expectOne(r => r.url === `${BASE}/dmrp/measure-mappings/search`);
    expect(req.request.params.get('pageNumber')).toBe('1');

    req.flush({
      records: [{ id: 'mm-1', measure: 'ACH', dqm: 'NHSNAcuteCareHospitalDailyInitialPopulation', frequency: Frequency.Monthly }],
      metadata: { pageSize: 10, pageNumber: 1, totalCount: 1, totalPages: 1 }
    });

    expect(result!.metadata.pageNumber).toBe(0);
  });

  it('omits empty filter params but includes populated ones', () => {
    service.searchMeasureMappings('ACH', 'dqm-1', Frequency.Weekly, 'DQM', 1, 25, 2).subscribe();

    const req = http.expectOne(r => r.url === `${BASE}/dmrp/measure-mappings/search`);
    expect(req.request.params.get('measure')).toBe('ACH');
    expect(req.request.params.get('dqm')).toBe('dqm-1');
    expect(req.request.params.get('frequency')).toBe('Weekly');
    expect(req.request.params.get('sortBy')).toBe('DQM');
    expect(req.request.params.get('sortOrder')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    expect(req.request.params.get('pageNumber')).toBe('3');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('passes a null body through unchanged for an empty search result', () => {
    let result: IPagedMeasureMapping | null | undefined = undefined;
    service.searchMeasureMappings('', '', null, 'Measure', 0, 10, 0).subscribe(r => (result = r));

    http.expectOne(r => r.url === `${BASE}/dmrp/measure-mappings/search`)
      .flush(null, { status: 204, statusText: 'No Content' });

    expect(result).toBeNull();
  });

  it('creates a measure mapping by posting to the base route', () => {
    service.createMeasureMapping({ id: '', measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Monthly }).subscribe();

    const req = http.expectOne(`${BASE}/dmrp/measure-mappings`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Monthly });
    req.flush({});
  });

  it('updates a measure mapping by id', () => {
    const mapping: IMeasureMapping = { id: 'mm-1', measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Daily };

    service.updateMeasureMapping(mapping).subscribe();

    const req = http.expectOne(`${BASE}/dmrp/measure-mappings/mm-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Daily });
    req.flush({});
  });

  it('deletes a measure mapping by id', () => {
    service.deleteMeasureMapping('mm-1').subscribe();

    const req = http.expectOne(`${BASE}/dmrp/measure-mappings/mm-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });

  it('suppresses the global toastr when a save fails', () => {
    failRequest(
      () => service.createMeasureMapping({ id: '', measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Monthly }).subscribe({ error: () => {} }),
      'POST', `${BASE}/dmrp/measure-mappings`);
    expect(toastrShown()).toBeFalse();

    failRequest(
      () => service.updateMeasureMapping({ id: 'mm-1', measure: 'ACH', dqm: 'dqm-1', frequency: Frequency.Monthly }).subscribe({ error: () => {} }),
      'PUT', `${BASE}/dmrp/measure-mappings/mm-1`);
    expect(toastrShown()).toBeFalse();
  });

  it('keeps the global toastr for search and delete failures', () => {
    service.searchMeasureMappings('', '', null, 'Measure', 0, 10, 0).subscribe({ error: () => {} });
    http.expectOne(r => r.method === 'GET' && r.url === `${BASE}/dmrp/measure-mappings/search`)
      .flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
    expect(toastrShown()).toBeTrue();

    failRequest(
      () => service.deleteMeasureMapping('mm-1').subscribe({ error: () => {} }),
      'DELETE', `${BASE}/dmrp/measure-mappings/mm-1`);
    expect(toastrShown()).toBeTrue();
  });
});
