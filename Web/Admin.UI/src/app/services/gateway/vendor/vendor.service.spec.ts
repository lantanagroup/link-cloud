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

  it('puts the vendor, secret id included, to the update route', () => {
    const vendor: IVendorConfigModel = { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' };

    service.updateVendor(vendor).subscribe();

    const req = http.expectOne(`${BASE}/normalization/Vendor/vendor-1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(vendor);
    req.flush({ success: true, message: '' });
  });

  it('sends a cleared secret id as null rather than omitting it', () => {
    service.updateVendor({ id: 'vendor-1', name: 'Epic', secretId: null }).subscribe();

    const req = http.expectOne(`${BASE}/normalization/Vendor/vendor-1`);
    expect(JSON.parse(JSON.stringify(req.request.body)).secretId).toBeNull();
    req.flush({ success: true, message: '' });
  });

  it('escapes vendor names in the create route', () => {
    service.createVendor('Acme / Health').subscribe();

    const req = http.expectOne(`${BASE}/normalization/Vendor/Acme%20%2F%20Health`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  // The config dialog shows its own snackbar for a failed save and stays open. Letting
  // ErrorHandlingService also raise its toastr would report one failure twice.
  it('suppresses the global toastr when a save fails', () => {
    failRequest(
      () => service.updateVendor({ id: 'vendor-1', name: 'Epic' }).subscribe({ error: () => {} }),
      'PUT', `${BASE}/normalization/Vendor/vendor-1`);
    expect(toastrShown()).toBeFalse();

    failRequest(
      () => service.createVendor('Veradigm').subscribe({ error: () => {} }),
      'POST', `${BASE}/normalization/Vendor/Veradigm`);
    expect(toastrShown()).toBeFalse();
  });

  // No dialog is open for these, so the toastr is the only thing that would tell the admin.
  it('keeps the global toastr for list and delete failures', () => {
    failRequest(
      () => service.getVendors().subscribe({ error: () => {} }),
      'GET', `${BASE}/normalization/Vendor/vendors`);
    expect(toastrShown()).toBeTrue();

    failRequest(
      () => service.deleteVendor('vendor-1').subscribe({ error: () => {} }),
      'DELETE', `${BASE}/normalization/Vendor/vendor-1`);
    expect(toastrShown()).toBeTrue();
  });
});
