import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom } from 'rxjs';

import { DMRP_OFF, DmrpStatusService } from './dmrp-status.service';
import { AppConfigService } from '../../app-config.service';

const BASE = 'http://link.test/api';
const STATUS_URL = `${BASE}/dmrp/dmrp-status`;

describe('DmrpStatusService', () => {
  let service: DmrpStatusService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AppConfigService, useValue: { config: { baseApiUrl: BASE } } }
      ]
    });

    service = TestBed.inject(DmrpStatusService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads both flags from Tenant', async () => {
    const status = firstValueFrom(service.getStatus());
    http.expectOne(STATUS_URL).flush({ dmrpEnabled: true, mockDmrpEnabled: true });

    expect(await status).toEqual({ dmrpEnabled: true, mockDmrpEnabled: true });
  });

  it('asks Tenant once for the whole session', async () => {
    const first = firstValueFrom(service.getStatus());
    http.expectOne(STATUS_URL).flush({ dmrpEnabled: true, mockDmrpEnabled: false });
    await first;

    expect(await firstValueFrom(service.getStatus())).toEqual({ dmrpEnabled: true, mockDmrpEnabled: false });
    http.expectNone(STATUS_URL);
  });

  it('never reports the mock on while DMRP is off', async () => {
    const status = firstValueFrom(service.getStatus());
    http.expectOne(STATUS_URL).flush({ dmrpEnabled: false, mockDmrpEnabled: true });

    expect(await status).toEqual(DMRP_OFF);
  });

  it('reads a failed call as DMRP off and asks again next time', async () => {
    const failed = firstValueFrom(service.getStatus());
    http.expectOne(STATUS_URL).flush({ detail: 'nope' }, { status: 502, statusText: 'Bad Gateway' });
    expect(await failed).toEqual(DMRP_OFF);

    const retried = firstValueFrom(service.getStatus());
    http.expectOne(STATUS_URL).flush({ dmrpEnabled: true, mockDmrpEnabled: true });
    expect(await retried).toEqual({ dmrpEnabled: true, mockDmrpEnabled: true });
  });
});
