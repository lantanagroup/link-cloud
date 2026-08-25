import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { DmrpGuard } from './dmrp.guard';
import { AppConfig, AppConfigService } from '../app-config.service';

describe('DmrpGuard', () => {
  let guard: DmrpGuard;
  let router: jasmine.SpyObj<Router>;
  let appConfigService: AppConfigService;

  beforeEach(() => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

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
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('redirects to the dashboard when the DMRP module is disabled', () => {
    appConfigService.config = { dmrpEnabled: false } as AppConfig;

    expect(guard.canActivate()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('treats a missing flag as disabled', () => {
    appConfigService.config = {} as AppConfig;

    expect(guard.canActivate()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
