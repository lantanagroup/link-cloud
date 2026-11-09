import {ActivatedRoute} from '@angular/router';
import {BehaviorSubject, of, Subject, throwError} from 'rxjs';
import {IReportCategoryCounts, ScheduleStatus} from '../../tenant/facility-view/report-view.interface';
import {PageEvent} from '@angular/material/paginator';
import {SubPreQualReportIssuesComponent} from './sub-pre-qual-report-issues.component';
import {FacilityViewService} from '../../tenant/facility-view/facility-view.service';
import {UNCATEGORIZED_GUIDANCE} from '../validation-category.constants';

describe('SubPreQualReportIssuesComponent', () => {
  let component: SubPreQualReportIssuesComponent;
  let reports: jasmine.SpyObj<FacilityViewService>;
  let routeParams: BehaviorSubject<{facilityId: string; submissionId: string; categoryId: string}>;
  beforeEach(() => {
    reports = jasmine.createSpyObj('FacilityViewService', ['getReportSummary', 'getReportCategoryCounts',
      'getCategoryIssuesPage']);
    reports.getReportSummary.and.returnValue(of({id: 'r'} as any));
    reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'f', reportScheduleId: 'r', lastUpdated: '',
      reportStatus: ScheduleStatus.Submitted, inProgress: false,
      categories: [{categoryId: 'c', title: 'Stored title', acceptable: false, submit: true, issues: 1000}]}));
    reports.getCategoryIssuesPage.and.returnValue(of({records: [], metadata: {pageNumber: 1, pageSize: 25, totalCount: 50, totalPages: 2}}));
    routeParams = new BehaviorSubject({facilityId: 'f', submissionId: 'r', categoryId: 'c'});
    component = new SubPreQualReportIssuesComponent(
      {params: routeParams} as unknown as ActivatedRoute,
      reports);
  });
  afterEach(() => component.ngOnDestroy());

  it('requests only the selected category and page, keeping stored and available counts distinct', () => {
    component.ngOnInit();
    expect(reports.getCategoryIssuesPage).toHaveBeenCalledWith('f', 'r', 'c', 1, 25);
    expect(component.issueCount).toBe(1000);
    expect(component.availableCount).toBe(50);
    expect(component.category?.title).toBe('Stored title');
    component.changePage({pageIndex: 1, pageSize: 25} as PageEvent);
    expect(reports.getCategoryIssuesPage).toHaveBeenCalledWith('f', 'r', 'c', 2, 25);
    expect(component.isLoading).toBeFalse();
    expect(reports.getReportSummary).toHaveBeenCalledTimes(1);
    expect(reports.getReportCategoryCounts).toHaveBeenCalledTimes(1);
  });

  it('stops the spinner and displays an error on failure', () => {
    reports.getCategoryIssuesPage.and.returnValue(throwError(() => ({status: 503})));
    component.ngOnInit();
    expect(component.isLoading).toBeFalse();
    expect(component.loadError).toBeDefined();
  });

  it('supports categories with stored counts but no saved findings', () => {
    reports.getCategoryIssuesPage.and.returnValue(of({records: [], metadata: {pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0}}));
    component.ngOnInit();
    expect(component.issueCount).toBe(1000);
    expect(component.availableCount).toBe(0);
    expect(component.reportIssues).toEqual([]);
    expect(component.loadError).toBeUndefined();
  });

  it('keeps pending metadata requests when a page changes', () => {
    const counts = new Subject<IReportCategoryCounts>();
    reports.getReportCategoryCounts.and.returnValue(counts);
    component.ngOnInit();
    component.changePage({pageIndex: 1, pageSize: 50} as PageEvent);
    expect(reports.getCategoryIssuesPage).toHaveBeenCalledWith('f', 'r', 'c', 2, 50);
    expect(reports.getReportCategoryCounts).toHaveBeenCalledTimes(1);
    expect(reports.getReportSummary).toHaveBeenCalledTimes(1);
    counts.next({facilityId: 'f', reportScheduleId: 'r', lastUpdated: '', categories: [],
      reportStatus: ScheduleStatus.Submitted, inProgress: false});
    counts.complete();
    expect(component.isLoading).toBeFalse();
    expect(component.pageIndex).toBe(1);
  });

  it('reloads metadata and resets paging when the route changes', () => {
    component.ngOnInit();
    component.changePage({pageIndex: 1, pageSize: 50} as PageEvent);
    routeParams.next({facilityId: 'f', submissionId: 'another-report', categoryId: 'another-category'});
    expect(reports.getReportSummary).toHaveBeenCalledTimes(2);
    expect(reports.getReportCategoryCounts).toHaveBeenCalledTimes(2);
    expect(reports.getCategoryIssuesPage).toHaveBeenCalledWith('f', 'another-report', 'another-category', 1, 25);
    expect(component.pageIndex).toBe(0);
    expect(component.pageSize).toBe(25);
  });

  it('can load another page after a failed issue request without reloading metadata', () => {
    reports.getCategoryIssuesPage.and.returnValues(throwError(() => ({status: 503})),
      of({records: [], metadata: {pageNumber: 2, pageSize: 25, totalCount: 50, totalPages: 2}}));
    component.ngOnInit();
    expect(component.loadError).toBeDefined();
    component.changePage({pageIndex: 1, pageSize: 25} as PageEvent);
    expect(component.loadError).toBeUndefined();
    expect(component.isLoading).toBeFalse();
    expect(reports.getReportCategoryCounts).toHaveBeenCalledTimes(1);
  });

  it('loads saved findings when stored counts are unavailable without inventing reported totals', () => {
    reports.getReportCategoryCounts.and.returnValue(throwError(() => ({status: 404})));
    reports.getCategoryIssuesPage.and.returnValue(of({records: [{message: 'Saved finding'} as any],
      metadata: {pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1}}));
    component.ngOnInit();
    expect(component.reportSummary?.id).toBe('r');
    expect(component.reportIssues[0].message).toBe('Saved finding');
    expect(component.countsUnavailable).toBeTrue();
    expect(component.issueCount).toBeUndefined();
    expect(component.availableCount).toBe(1);
    expect(component.loadError).toBeUndefined();
  });

  [true, false].forEach(hasCategories => {
    it(`marks counts as provisional while running, categories present: ${hasCategories}`, () => {
      reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'f', reportScheduleId: 'r', lastUpdated: '',
        reportStatus: ScheduleStatus.New, inProgress: true,
        categories: hasCategories ? [{categoryId: 'c', title: 'Partial', acceptable: false, submit: true,
          issues: 3, guidance: 'BFF guidance', severity: 'WARNING'}] : []}));
      component.ngOnInit();
      expect(component.inProgress).toBeTrue();
      expect(component.issueCount).toBe(hasCategories ? 3 : 0);
      expect(component.category?.guidance).toBe(hasCategories ? 'BFF guidance' : '');
    });
  });

  it('keeps counts outages as errors', () => {
    reports.getReportCategoryCounts.and.returnValue(throwError(() => ({status: 503})));
    component.ngOnInit();
    expect(component.loadError).toBeDefined();
    expect(component.countsUnavailable).toBeFalse();
  });

  it('keeps report summary failures as errors', () => {
    reports.getReportSummary.and.returnValue(throwError(() => ({status: 404})));
    component.ngOnInit();
    expect(component.loadError).toBeDefined();
  });

  it('restores uncategorized guidance on direct navigation', () => {
    routeParams.next({facilityId: 'f', submissionId: 'r', categoryId: 'UNCATEGORIZED'});
    component.ngOnInit();
    expect(component.category?.guidance).toBe(UNCATEGORIZED_GUIDANCE);
  });
});
