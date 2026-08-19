import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { throwError } from 'rxjs';

import { VendorService } from './vendor.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { AppConfigService } from '../../app-config.service';
import { IVendorConfigModel } from '../../../interfaces/vendor/vendor-config-model.interface';

const BASE = 'http://link.test/api';

describe('VendorService', () => {
  let service: VendorService;
  let http: HttpTestingController;
  let errorHandler: jasmine.SpyObj<ErrorHandlingService>;

  beforeEach(() => {
    errorHandler = jasmine.createSpyObj<ErrorHandlingService>('ErrorHandlingService', ['handleError']);
    // Mirrors the real handler, which stamps a sanitized message onto the error and rethrows it.
    errorHandler.handleError.and.callFake((err: any) => throwError(() => err));

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ErrorHandlingService, useValue: errorHandler },
        { provide: AppConfigService, useValue: { config: { baseApiUrl: BASE } } }
      ]
    });

    service = TestBed.inject(VendorService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Subscribes, swallows the rethrown error, and flushes a failure for the matching request. */
  function failRequest(call: () => void, method: string, url: string): void {
    call();
    http.expectOne(r => r.method === method && r.url === url)
      .flush({ detail: 'nope' }, { status: 500, statusText: 'Server Error' });
  }

  /** The second argument to handleError is `genericToaster`; absent means it defaults to true. */
  function toastrShown(): boolean {
    return errorHandler.handleError.calls.mostRecent().args[1] !== false;
  }

  it('lists vendors from the tenant route', () => {
    service.getVendors().subscribe();

    const req = http.expectOne(`${BASE}/vendor`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('flattens the nested signing key secret id when listing', () => {
    let vendors: IVendorConfigModel[] = [];
    service.getVendors().subscribe(result => (vendors = result));

    http.expectOne(`${BASE}/vendor`).flush([
      { id: 'vendor-1', name: 'Epic', authentication: { signingKeySecretId: 'epic-signing-pem' } },
      { id: 'vendor-2', name: 'Cerner', authentication: null }
    ]);

    expect(vendors).toEqual([
      { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' },
      { id: 'vendor-2', name: 'Cerner', secretId: null }
    ]);
  });

  it('creates a vendor by posting a body to the tenant route', () => {
    service.createVendor({ id: '', name: 'Acme / Health', secretId: null }).subscribe();

    const req = http.expectOne(`${BASE}/vendor`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Acme / Health',
      authentication: { signingKeySecretId: null }
    });
    req.flush({});
  });

  it('creates a vendor with the secret id entered on the add form', () => {
    service.createVendor({ id: '', name: 'Epic', secretId: 'epic-signing-pem' }).subscribe();

    const req = http.expectOne(`${BASE}/vendor`);
    expect(req.request.body).toEqual({
      name: 'Epic',
      authentication: { signingKeySecretId: 'epic-signing-pem' }
    });
    req.flush({});
  });

  it('puts the vendor with its secret id nested under authentication', () => {
    const vendor: IVendorConfigModel = { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' };

    service.updateVendor(vendor).subscribe();

    const req = http.expectOne(`${BASE}/vendor/vendor-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      name: 'Epic',
      authentication: { signingKeySecretId: 'epic-signing-pem' }
    });
    req.flush({ success: true, message: '' });
  });

  it('sends a cleared secret id as an explicit null inside authentication', () => {
    service.updateVendor({ id: 'vendor-1', name: 'Epic', secretId: null }).subscribe();

    const req = http.expectOne(`${BASE}/vendor/vendor-1`);
    const body = JSON.parse(JSON.stringify(req.request.body));
    expect(body.authentication).toEqual({ signingKeySecretId: null });
    req.flush({ success: true, message: '' });
  });

  it('deletes a vendor through the tenant route', () => {
    service.deleteVendor('vendor-1').subscribe();

    const req = http.expectOne(`${BASE}/vendor/vendor-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });

  // The config dialog shows its own snackbar for a failed save and stays open. Letting
  // ErrorHandlingService also raise its toastr would report one failure twice.
  it('suppresses the global toastr when a save fails', () => {
    failRequest(
      () => service.updateVendor({ id: 'vendor-1', name: 'Epic' }).subscribe({ error: () => {} }),
      'PUT', `${BASE}/vendor/vendor-1`);
    expect(toastrShown()).toBeFalse();

    failRequest(
      () => service.createVendor({ id: '', name: 'Veradigm' }).subscribe({ error: () => {} }),
      'POST', `${BASE}/vendor`);
    expect(toastrShown()).toBeFalse();
  });

  // No dialog is open for these, so the toastr is the only thing that would tell the admin.
  it('keeps the global toastr for list and delete failures', () => {
    failRequest(
      () => service.getVendors().subscribe({ error: () => {} }),
      'GET', `${BASE}/vendor`);
    expect(toastrShown()).toBeTrue();

    failRequest(
      () => service.deleteVendor('vendor-1').subscribe({ error: () => {} }),
      'DELETE', `${BASE}/vendor/vendor-1`);
    expect(toastrShown()).toBeTrue();
  });
});
