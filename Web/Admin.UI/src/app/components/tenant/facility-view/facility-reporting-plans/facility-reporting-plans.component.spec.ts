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

  describe('filtering', () => {
    beforeEach(() => fixture.detectChanges());

    it('filters by measure or dQM text, case-insensitively', () => {
      component.filterText = 'ach';
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-1']);

      component.filterText = 'HOBD';
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-2']);
    });

    it('filters by reporting period', () => {
      component.filterPeriod = '2026-3';
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-1']);
    });

    it('filters by reporting state', () => {
      component.filterReporting = false;
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-2']);
    });

    it('filters by reporting cadence', () => {
      component.filterFrequency = Frequency.Monthly;
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-1']);

      component.filterFrequency = Frequency.Daily;
      component.onFilterChange();
      expect(component.dataSource.data.map(p => p.id)).toEqual(['rp-2']);
    });

    it('offers the cadences present in the data, in canonical order', () => {
      expect(component.frequencyOptions).toEqual([Frequency.Daily, Frequency.Monthly]);
    });

    it('offers the distinct periods newest first', () => {
      expect(component.periodOptions.map(o => o.label)).toEqual(['April 2026', 'March 2026']);
    });

    it('shows a no-match state distinct from the empty state when filters exclude everything', () => {
      component.filterText = 'ACH';
      component.filterReporting = false;
      component.onFilterChange();
      fixture.detectChanges();

      expect(component.dataSource.data.length).toBe(0);
      expect(fixture.nativeElement.querySelector('[data-testid="reporting-plans-no-match"]')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="reporting-plans-empty"]')).toBeNull();
    });

    it('clearFilters restores the full list', () => {
      component.filterText = 'ach';
      component.filterReporting = false;
      component.filterFrequency = Frequency.Daily;
      component.onFilterChange();

      component.clearFilters();

      expect(component.hasActiveFilters()).toBeFalse();
      expect(component.dataSource.data.length).toBe(2);
    });
  });

  describe('pagination', () => {
    const manyPlans: IFacilityReportingPlan[] = Array.from({ length: 15 }, (_, i) => ({
      id: `rp-${i}`, facilityId: '100', measureMappingId: `mm-${i}`,
      reportingMonth: (i % 12) + 1, reportingYear: 2026, isReporting: true,
      measure: `Measure${i}`, dqm: `Dqm${i}`, frequency: Frequency.Monthly,
      createDate: '2026-01-01T00:00:00Z', modifyDate: null
    }));

    beforeEach(() => {
      reportingPlanService.getReportingPlansForFacility.and.returnValue(of(manyPlans));
      fixture.detectChanges();
    });

    it('pages the table at 10 rows by default', () => {
      expect(component.dataSource.paginator).toBeTruthy();
      const rows = fixture.nativeElement.querySelectorAll('tr.mat-mdc-row:not(.mat-mdc-no-data-row)');
      expect(rows.length).toBe(10);
    });

    it('resets to the first page when a filter changes', () => {
      component.dataSource.paginator!.pageIndex = 1;

      component.filterText = 'Measure1';
      component.onFilterChange();

      expect(component.dataSource.paginator!.pageIndex).toBe(0);
    });
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


