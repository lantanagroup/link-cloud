import { TestBed } from '@angular/core/testing';

import { DataAcquisitionService } from './data-acquisition.service';

describe('DataAcquisitionService', () => {
  let service: DataAcquisitionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(DataAcquisitionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
