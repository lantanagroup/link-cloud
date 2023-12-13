import { TestBed } from '@angular/core/testing';

import { TenantServiceService } from './tenant.service';

describe('TenantServiceService', () => {
  let service: TenantServiceService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TenantServiceService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
