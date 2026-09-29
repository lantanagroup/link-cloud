import {ComponentFixture, TestBed} from '@angular/core/testing';
import {NoopAnimationsModule} from '@angular/platform-browser/animations';
import {of, Subject} from 'rxjs';
import {MonitorService} from '../monitor.service';
import {ServiceInfoService} from '../service-info.service';
import {LinkHealthCheckComponent} from './link-health-check.component';
import {ILinkServiceHealthSummary} from './link-service-health-summary.interface';

describe('LinkHealthCheckComponent', () => {
  let fixture: ComponentFixture<LinkHealthCheckComponent>;
  let component: LinkHealthCheckComponent;
  let monitorService: jasmine.SpyObj<MonitorService>;
  let accountResponse: Subject<ILinkServiceHealthSummary>;

  const accountReport: ILinkServiceHealthSummary = {
    service: 'Account',
    status: 'Healthy',
    totalDuration: '00:00:00.001',
    entries: {}
  };

  beforeEach(async () => {
    accountResponse = new Subject<ILinkServiceHealthSummary>();
    monitorService = jasmine.createSpyObj<MonitorService>('MonitorService', ['getServiceHealthCheck']);
    monitorService.getServiceHealthCheck.and.callFake(service => service === 'account'
      ? accountResponse
      : of({...accountReport, service}));
    const serviceInfoService = jasmine.createSpyObj<ServiceInfoService>('ServiceInfoService', ['getServiceInfos']);
    serviceInfoService.getServiceInfos.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [LinkHealthCheckComponent, NoopAnimationsModule],
      providers: [
        {provide: MonitorService, useValue: monitorService},
        {provide: ServiceInfoService, useValue: serviceInfoService}
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LinkHealthCheckComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function accountDetails(): HTMLTableRowElement {
    return (fixture.nativeElement as HTMLElement)
      .querySelector('#health-entries-account')!.closest('tr')!;
  }

  it('keeps a loading row expanded when its display name arrives', () => {
    component.toggleService('account');
    fixture.detectChanges();
    const originalRow = accountDetails();
    expect(originalRow.hidden).toBeFalse();

    accountResponse.next(accountReport);
    accountResponse.complete();
    fixture.detectChanges();

    expect(component.healthSummary[0].service).toBe('Account');
    expect(component.healthSummary[0].serviceKey).toBe('account');
    expect(accountDetails()).toBe(originalRow);
    expect(accountDetails().hidden).toBeFalse();
    expect(component.initializeSummaries).toBeFalse();
  });

  it('preserves expansion throughout refresh and requests the stable service key', () => {
    accountResponse.next(accountReport);
    accountResponse.complete();
    component.toggleService('account');
    fixture.detectChanges();

    accountResponse = new Subject<ILinkServiceHealthSummary>();
    component.refreshHealthSummary();
    fixture.detectChanges();

    expect(component.initializeSummaries).toBeTrue();
    expect(accountDetails().hidden).toBeFalse();
    expect(monitorService.getServiceHealthCheck).not.toHaveBeenCalledWith('Account');
    expect(monitorService.getServiceHealthCheck.calls.allArgs()
      .filter(args => args[0] === 'account').length).toBe(2);

    accountResponse.next(accountReport);
    accountResponse.complete();
    fixture.detectChanges();

    expect(accountDetails().hidden).toBeFalse();
    expect(component.initializeSummaries).toBeFalse();
  });
});