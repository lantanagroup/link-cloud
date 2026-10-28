import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { TenantService } from './tenant.service';
import { AppConfigService } from '../../app-config.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { IVendor, IVendorVersion } from '../../../interfaces/tenant/vendor-interface';

describe('TenantServiceService', () => {
  let service: TenantService;
  let httpTestingController: HttpTestingController;

  const baseApiUrl = 'https://example.test/api';
  const vendor: IVendor = { id: 'vendor-id', name: 'Epic' };
  const vendorVersion: IVendorVersion = { id: 'vendor-version-id', vendorId: vendor.id, vendorName: vendor.name, version: '2026.1' };
  const scheduledReports = { daily: [], monthly: [], weekly: [] };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AppConfigService, useValue: { config: { baseApiUrl } } },
        { provide: ErrorHandlingService, useValue: { handleError: jasmine.createSpy('handleError') } }
      ]
    });
    service = TestBed.inject(TenantService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('gets vendors from the Tenant API', () => {
    let response: IVendor[] | undefined;

    service.getVendors().subscribe(vendors => response = vendors);

    const request = httpTestingController.expectOne(`${baseApiUrl}/vendor`);
    expect(request.request.method).toBe('GET');
    request.flush([vendor]);

    expect(response).toEqual([vendor]);
  });

  it('sends the selected vendor version when creating a facility', () => {
    service.createFacility('facility-id', 'Facility', 'UTC', scheduledReports, vendorVersion).subscribe();

    const request = httpTestingController.expectOne(`${baseApiUrl}/facility`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.vendor).toEqual(vendor);
    expect(request.request.body.vendorVersionId).toBe(vendorVersion.id);
    request.flush({ id: 'facility-id', message: '' });
  });

  it('filters facilities by vendor ID', () => {
    service.listFacilities('', '', '', vendor.id, 'FacilityId', 0, 10, 0, false).subscribe();

    const request = httpTestingController.expectOne(
      candidate => candidate.url === `${baseApiUrl}/facility` && candidate.params.get('vendor.id') === vendor.id
    );
    expect(request.request.params.has('vendor')).toBeFalse();
    request.flush({ records: [], metadata: { pageNumber: 1 } });
  });
});
