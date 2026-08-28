import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';

import { FacilityReportingPlansComponent } from './facility-reporting-plans.component';
import { ReportingPlanService } from '../../../../services/gateway/dmrp/reporting-plan.service';
import { Frequency } from '../../../../interfaces/dmrp/measure-mapping.interface';
import { IFacilityReportingPlan } from '../../../../interfaces/dmrp/facility-reporting-plan.interface';

describe('FacilityReportingPlansComponent', () => {
  let component: FacilityReportingPlansComponent;
  let fixture: ComponentFixture<FacilityReportingPlansComponent>;
  let reportingPlanService: jasmine.SpyObj<ReportingPlanService>;

  const plans: IFacilityReportingPlan[] = [
    {
      id: 'rp-1', facilityId: '100', measureMappingId: 'mm-1',
      reportingMonth: 3, reportingYear: 2026, isReporting: true,
      measure: 'ACH', dqm: 'AchMonthly', frequency: Frequency.Monthly,
      createDate: '2026-02-28T00:00:00Z', modifyDate: null
    },
    {
      id: 'rp-2', facilityId: '100', measureMappingId: 'mm-2',
      reportingMonth: 4, reportingYear: 2026, isReporting: false,
      measure: 'HOB', dqm: 'HobDaily', frequency: Frequency.Daily,
      createDate: '2026-03-28T00:00:00Z', modifyDate: '2026-04-02T00:00:00Z'
    }
  ];

  beforeEach(async () => {
    reportingPlanService = jasmine.createSpyObj<ReportingPlanService>(
      'ReportingPlanService', ['getReportingPlansForFacility']);
    reportingPlanService.getReportingPlansForFacility.and.returnValue(of(plans));

    await TestBed.configureTestingModule({
      imports: [FacilityReportingPlansComponent, NoopAnimationsModule],
      providers: [
        { provide: ReportingPlanService, useValue: reportingPlanService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FacilityReportingPlansComponent);
    component = fixture.componentInstance;
    component.facilityId = '100';
  });

  it('loads the facility\'s reporting plans, newest period first', () => {
    fixture.detectChanges();

    expect(reportingPlanService.getReportingPlansForFacility).toHaveBeenCalledWith('100');
    expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-2', 'rp-1']);
    expect(component.loading).toBeFalse();
  });

  it('labels the rows readably', () => {
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const text = root.textContent ?? '';
    expect(text).toContain('ACH');
    expect(text).toContain('AchMonthly');
    expect(text).toContain('March 2026');

    // "No" would match too much as a substring, so assert on the rendered cells instead.
    const reportingCells = Array.from(root.querySelectorAll('td.mat-column-isReporting'))
      .map(cell => cell.textContent?.trim());
    expect(reportingCells).toEqual(['No', 'Yes']);
  });

  it('shows the empty state when the facility has no plans', () => {
    reportingPlanService.getReportingPlansForFacility.and.returnValue(of([]));

    fixture.detectChanges();

    const empty = fixture.nativeElement.querySelector('[data-testid="reporting-plans-empty"]');
    expect(empty).not.toBeNull();
  });

  it('surfaces a failed load with a message and recovers on retry', () => {
    reportingPlanService.getReportingPlansForFacility.and.returnValue(throwError(() => new Error('down')));

    fixture.detectChanges();

    expect(component.loadFailed).toBeTrue();
    const error = fixture.nativeElement.querySelector('[data-testid="reporting-plans-error"]');
    expect(error).not.toBeNull();

    reportingPlanService.getReportingPlansForFacility.and.returnValue(of(plans));
    const retryButton: HTMLButtonElement =
      fixture.nativeElement.querySelector('[data-testid="reporting-plans-error"] button');
    retryButton.click();
    fixture.detectChanges();

    expect(component.loadFailed).toBeFalse();
    expect(component.dataSource.data.length).toBe(2);
  });
});
