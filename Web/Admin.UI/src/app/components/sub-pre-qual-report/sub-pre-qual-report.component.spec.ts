import {ActivatedRoute} from '@angular/router';
import {of, throwError} from 'rxjs';
import {SubPreQualReportComponent} from './sub-pre-qual-report.component';
import {FacilityViewService} from '../tenant/facility-view/facility-view.service';
import {ScheduleStatus} from '../tenant/facility-view/report-view.interface';

describe('SubPreQualReportComponent', () => {
  let component: SubPreQualReportComponent;
  let reports: jasmine.SpyObj<FacilityViewService>;

  beforeEach(() => {
    reports = jasmine.createSpyObj('FacilityViewService', ['getReportSummary', 'getReportCategoryCounts', 'downloadValidationResults']);
    reports.getReportSummary.and.returnValue(of({id: 'report-1'} as any));
    reports.getReportCategoryCounts.and.returnValue(of({
      facilityId: 'facility-1', reportScheduleId: 'report-1', lastUpdated: '2026-11-04T12:00:00Z',
      reportStatus: ScheduleStatus.Submitted, inProgress: false,
      categories: [{categoryId: 'category-1', title: 'Stored title', acceptable: false, submit: false,
        issues: 3825417, guidance: 'Review findings', severity: 'WARNING'}]
    }));
    component = new SubPreQualReportComponent(
      {params: of({facilityId: 'facility-1', submissionId: 'report-1'})} as unknown as ActivatedRoute,
      reports
    );
  });

  afterEach(() => component.ngOnDestroy());

  it('loads stored category counts without downloading or recategorizing findings', () => {
    component.ngOnInit();
    expect(reports.getReportCategoryCounts).toHaveBeenCalledWith('report-1');
    expect(reports.getReportSummary).toHaveBeenCalledWith('facility-1', 'report-1', true);
    expect(component.categoryCounts[0]).toEqual(jasmine.objectContaining({
      title: 'Stored title', acceptable: false, submit: false, issues: 3825417, guidance: 'Review findings'
    }));
    expect(component.reportIssuesSummary).toEqual([{value: 'Stored title', count: 3825417}]);
    expect(component.isLoading).toBeFalse();
  });

  it('downloads findings for the current facility and report', () => {
    component.ngOnInit();
    component.downloadReport();
    expect(reports.downloadValidationResults).toHaveBeenCalledWith('facility-1', 'report-1');
  });

  it('keeps report details and download available when stored counts are missing', () => {
    reports.getReportCategoryCounts.and.returnValue(throwError(() => ({status: 404})));
    component.ngOnInit();
    expect(component.isLoading).toBeFalse();
    expect(component.loadError).toBeUndefined();
    expect(component.reportSummary?.id).toBe('report-1');
    expect(component.countsUnavailable).toBeTrue();
    expect(component.reportIssuesSummary).toBeUndefined();
    component.downloadReport();
    expect(reports.downloadValidationResults).toHaveBeenCalled();
  });

  it('shows counts when category guidance is unavailable', () => {
    reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'facility-1', reportScheduleId: 'report-1',
      lastUpdated: '', reportStatus: null, inProgress: false,
      categories: [{categoryId: 'c', title: 'Stored title', acceptable: false, submit: true, issues: 3825417,
        guidance: null, severity: null}]}));
    component.ngOnInit();
    expect(component.categoryCounts[0].issues).toBe(3825417);
    expect(component.categoryCounts[0].guidance).toBeNull();
    expect(component.loadError).toBeUndefined();
  });

  it('shows an empty summary when the report has no findings', () => {
    reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'facility-1', reportScheduleId: 'report-1',
      reportStatus: ScheduleStatus.Submitted, inProgress: false, lastUpdated: '', categories: []}));
    component.ngOnInit();
    expect(component.categoryCounts).toEqual([]);
    expect(component.reportIssuesSummary).toEqual([]);
    expect(component.isLoading).toBeFalse();
  });

  [true, false].forEach(hasCategories => {
    it(`marks counts as provisional while running, categories present: ${hasCategories}`, () => {
      reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'facility-1', reportScheduleId: 'report-1',
        reportStatus: ScheduleStatus.New, inProgress: true, lastUpdated: '',
        categories: hasCategories ? [{categoryId: 'c', title: 'Partial', acceptable: false, submit: true, issues: 3}] : []}));
      component.ngOnInit();
      expect(component.inProgress).toBeTrue();
      expect(component.countsUnavailable).toBeFalse();
      expect(component.categoryCounts.length).toBe(hasCategories ? 1 : 0);
    });
  });

  it('shows an error for a counts outage', () => {
    reports.getReportCategoryCounts.and.returnValue(throwError(() => ({status: 500})));
    component.ngOnInit();
    expect(component.loadError).toBeDefined();
    expect(component.countsUnavailable).toBeFalse();
  });

  it('does not suppress a missing report summary', () => {
    reports.getReportSummary.and.returnValue(throwError(() => ({status: 404})));
    component.ngOnInit();
    expect(component.loadError).toBeDefined();
    expect(component.reportSummary).toBeUndefined();
  });
});
