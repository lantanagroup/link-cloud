import {ActivatedRoute} from '@angular/router';
import {of, throwError} from 'rxjs';
import {PageEvent} from '@angular/material/paginator';
import {SubPreQualReportIssuesComponent} from './sub-pre-qual-report-issues.component';
import {FacilityViewService} from '../../tenant/facility-view/facility-view.service';
import {ValidationService} from '../../../services/gateway/validation/validation.service';

describe('SubPreQualReportIssuesComponent', () => {
  let component: SubPreQualReportIssuesComponent;
  let reports: jasmine.SpyObj<FacilityViewService>;
  beforeEach(() => {
    reports = jasmine.createSpyObj('FacilityViewService', ['getReportSummary', 'getReportCategoryCounts',
      'getCategoryIssuesPage']);
    reports.getReportSummary.and.returnValue(of({id: 'r'} as any));
    reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'f', reportId: 'r', lastUpdated: '',
      categories: [{categoryId: 'c', title: 'Stored title', acceptable: false, submit: true, issues: 1000}]}));
    reports.getCategoryIssuesPage.and.returnValue(of({records: [], metadata: {pageNumber: 1, pageSize: 25, totalCount: 50, totalPages: 2}}));
    const validation = jasmine.createSpyObj('ValidationService', ['getValidationCategories']);
    validation.getValidationCategories.and.returnValue(of([]));
    component = new SubPreQualReportIssuesComponent(
      {params: of({facilityId: 'f', submissionId: 'r', categoryId: 'c'})} as unknown as ActivatedRoute,
      reports, validation);
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
});
