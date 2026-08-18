import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { OperationService } from './operation.service';
import { AppConfigService } from '../../app-config.service';
import { ErrorHandlingService } from '../../error-handling.service';
import { IVendor, IVendorVersion } from '../../../interfaces/tenant/vendor-interface';
import { ISaveOperationModel } from '../../../interfaces/normalization/operation-save-model.interface';

describe('OperationService', () => {
  let service: OperationService;
  let httpTestingController: HttpTestingController;

  const baseApiUrl = 'https://example.test/api';
  const vendor: IVendor = { id: 'vendor-id', name: 'Epic' };
  const vendorVersion: IVendorVersion = { id: 'vendor-version-id', vendorId: vendor.id, vendorName: vendor.name, version: '2026.1' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AppConfigService, useValue: { config: { baseApiUrl } } },
        { provide: ErrorHandlingService, useValue: { handleError: jasmine.createSpy('handleError') } }
      ]
    });
    service = TestBed.inject(OperationService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('gets vendors from the Tenant API', () => {
    let response: IVendor[] | undefined;

    service.getVendors().subscribe(vendors => response = vendors);

    const request = httpTestingController.expectOne(`${baseApiUrl}/vendor`);
    expect(request.request.method).toBe('GET');
    request.flush([vendor]);

    expect(response).toEqual([vendor]);
  });

  it('gets vendor versions from the Tenant API', () => {
    let response: IVendorVersion[] | undefined;

    service.getVendorVersions().subscribe(vendorVersions => response = vendorVersions);

    const request = httpTestingController.expectOne(`${baseApiUrl}/VendorVersion`);
    expect(request.request.method).toBe('GET');
    request.flush([vendorVersion]);

    expect(response).toEqual([vendorVersion]);
  });

  it('sends vendor version IDs when creating an operation', () => {
    const operation: ISaveOperationModel = {
      resourceTypes: ['Patient'],
      vendorVersionIds: [vendorVersion.id],
      operation: {
        OperationType: 'CodeMap',
        Name: 'Map codes',
        Description: ''
      }
    };

    service.createOperationConfiguration(operation).subscribe();

    const request = httpTestingController.expectOne(`${baseApiUrl}/normalization/operations`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.vendorVersionIds).toEqual([vendorVersion.id]);
    expect(request.request.body.vendorIds).toBeUndefined();
    request.flush({ id: 'operation-id', message: '' });
  });
});