import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';

import { DmrpGuard } from './dmrp.guard';
import { AppConfig, AppConfigService } from '../app-config.service';

describe('DmrpGuard', () => {
  let guard: DmrpGuard;
  let router: jasmine.SpyObj<Router>;
  let appConfigService: AppConfigService;
  const dashboardTree = {} as UrlTree;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(dashboardTree);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: AppConfigService, useValue: { config: undefined } }
      ]
    });

    guard = TestBed.inject(DmrpGuard);
    appConfigService = TestBed.inject(AppConfigService);
  });

  it('allows activation when the DMRP module is enabled', () => {
    appConfigService.config = { dmrpEnabled: true } as AppConfig;

    expect(guard.canActivate()).toBeTrue();
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects to the dashboard when the DMRP module is disabled', () => {
    appConfigService.config = { dmrpEnabled: false } as AppConfig;

    expect(guard.canActivate()).toBe(dashboardTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
  });

  it('treats a missing flag as disabled', () => {
    appConfigService.config = {} as AppConfig;

    expect(guard.canActivate()).toBe(dashboardTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
  });
});
