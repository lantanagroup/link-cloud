import { TestBed } from '@angular/core/testing';

import { CensusService } from './census.service';

describe('CensusService', () => {
  let service: CensusService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CensusService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
