import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { firstValueFrom, of } from 'rxjs';

import { DmrpGuard } from './dmrp.guard';
import { DMRP_OFF, DmrpStatusService } from '../gateway/dmrp/dmrp-status.service';
import { IDmrpStatus } from '../../interfaces/dmrp/dmrp-status.interface';

describe('DmrpGuard', () => {
  let guard: DmrpGuard;
  let router: jasmine.SpyObj<Router>;
  let dmrpStatusService: jasmine.SpyObj<DmrpStatusService>;
  const dashboardTree = {} as UrlTree;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(dashboardTree);
    dmrpStatusService = jasmine.createSpyObj<DmrpStatusService>('DmrpStatusService', ['getStatus']);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: DmrpStatusService, useValue: dmrpStatusService }
      ]
    });

    guard = TestBed.inject(DmrpGuard);
  });

  function givenStatus(status: IDmrpStatus): void {
    dmrpStatusService.getStatus.and.returnValue(of(status));
  }

  it('allows activation when Tenant reports DMRP enabled', async () => {
    givenStatus({ dmrpEnabled: true, mockDmrpEnabled: false });

    expect(await firstValueFrom(guard.canActivate())).toBeTrue();
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects to the dashboard when Tenant reports DMRP disabled', async () => {
    givenStatus(DMRP_OFF);

    expect(await firstValueFrom(guard.canActivate())).toBe(dashboardTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
  });
});
