import {ActivatedRoute} from '@angular/router';
import {of, throwError} from 'rxjs';
import {SubPreQualReportComponent} from './sub-pre-qual-report.component';
import {FacilityViewService} from '../tenant/facility-view/facility-view.service';
import {ValidationService} from '../../services/gateway/validation/validation.service';

describe('SubPreQualReportComponent', () => {
  let component: SubPreQualReportComponent;
  let reports: jasmine.SpyObj<FacilityViewService>;
  let validation: jasmine.SpyObj<ValidationService>;

  beforeEach(() => {
    reports = jasmine.createSpyObj('FacilityViewService', ['getReportSummary', 'getReportCategoryCounts', 'getReportIssues', 'getReportIssuesSummary']);
    validation = jasmine.createSpyObj('ValidationService', ['getValidationCategories']);
    reports.getReportSummary.and.returnValue(of({id: 'report-1'} as any));
    reports.getReportCategoryCounts.and.returnValue(of({
      facilityId: 'facility-1', reportId: 'report-1', lastUpdated: '2026-11-04T12:00:00Z',
      categories: [{categoryId: 'category-1', title: 'Stored title', acceptable: false, submit: false, issues: 3825417}]
    }));
    validation.getValidationCategories.and.returnValue(of([
      {id: 'CATEGORY-1', title: 'Current title', acceptable: true, guidance: 'Review findings', severity: 'WARNING'}
    ]));
    component = new SubPreQualReportComponent(
      {params: of({facilityId: 'facility-1', submissionId: 'report-1'})} as unknown as ActivatedRoute,
      reports, validation
    );
  });

  afterEach(() => component.ngOnDestroy());

  it('loads stored category counts without downloading or recategorizing findings', () => {
    component.ngOnInit();
    expect(reports.getReportCategoryCounts).toHaveBeenCalledWith('facility-1', 'report-1');
    expect(reports.getReportIssues).not.toHaveBeenCalled();
    expect(reports.getReportIssuesSummary).not.toHaveBeenCalled();
    expect(component.categoryCounts[0]).toEqual(jasmine.objectContaining({
      title: 'Stored title', acceptable: false, submit: false, issues: 3825417, guidance: 'Review findings'
    }));
    expect(component.reportIssuesSummary).toEqual([{value: 'Stored title', count: 3825417}]);
    expect(component.isLoading).toBeFalse();
  });

  it('stops loading and shows an error when stored counts are missing', () => {
    reports.getReportCategoryCounts.and.returnValue(throwError(() => ({status: 404})));
    component.ngOnInit();
    expect(component.isLoading).toBeFalse();
    expect(component.loadError).toBeDefined();
    expect(reports.getReportIssues).not.toHaveBeenCalled();
  });

  it('shows counts when category guidance is unavailable', () => {
    validation.getValidationCategories.and.returnValue(throwError(() => ({status: 503})));
    component.ngOnInit();
    expect(component.categoryCounts[0].issues).toBe(3825417);
    expect(component.categoryCounts[0].guidance).toBe('');
    expect(component.loadError).toBeUndefined();
  });

  it('shows an empty summary when the report has no findings', () => {
    reports.getReportCategoryCounts.and.returnValue(of({facilityId: 'facility-1', reportId: 'report-1', lastUpdated: '', categories: []}));
    component.ngOnInit();
    expect(component.categoryCounts).toEqual([]);
    expect(component.reportIssuesSummary).toEqual([]);
    expect(component.isLoading).toBeFalse();
  });
});
