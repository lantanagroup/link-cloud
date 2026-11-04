import {Component, OnDestroy, OnInit} from '@angular/core';

import {ActivatedRoute, RouterLink, RouterLinkActive} from '@angular/router';
import {CommonModule} from '@angular/common';
import {FacilityViewService} from '../../tenant/facility-view/facility-view.service';
import {
  IReportListSummary,
  ICategoryIssue,
  IValidationIssueCategory
} from '../../tenant/facility-view/report-view.interface';
import {
  SubPreQualReportIssuesTableComponent
} from '../sub-pre-qual-report-issues-table/sub-pre-qual-report-issues-table.component';
import {SubPreQualReportMetaComponent} from '../sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import {catchError, forkJoin, of, Subject, Subscription, switchMap, startWith, tap} from 'rxjs';
import {PageEvent} from '@angular/material/paginator';
import {ValidationService} from '../../../services/gateway/validation/validation.service';
import {MatTabsModule} from '@angular/material/tabs';
import {MatIconModule} from '@angular/material/icon';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';

@Component({
  selector: 'app-sub-pre-qual-report-issues',
  imports: [
    CommonModule,
    SubPreQualReportMetaComponent,
    SubPreQualReportIssuesTableComponent,
    MatTabsModule,
    RouterLink,
    RouterLinkActive,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './sub-pre-qual-report-issues.component.html',
  styleUrls: ['./sub-pre-qual-report-issues.component.scss'],
  standalone: true
})
export class SubPreQualReportIssuesComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';
  categoryId: string = '';
  issueCount: number = 0;

  category: IValidationIssueCategory | undefined;
  reportIssues: ICategoryIssue[] = [];
  availableCount = 0;
  pageIndex = 0;
  pageSize = 25;
  loadError: string | undefined;
  private readonly pageChanges = new Subject<PageEvent>();
  reportSummary: IReportListSummary | undefined;
  isLoading: boolean = true;

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService,
    private validationService: ValidationService
  ) { }

  ngOnInit() {
    this.subscription = this.route.params.pipe(
      tap(params => {
        this.facilityId = params['facilityId'];
        this.submissionId = params['submissionId'];
        this.categoryId = params['categoryId'];
        this.category = undefined;
        this.issueCount = 0;
      }),
      switchMap(() => this.pageChanges.pipe(
        startWith({pageIndex: 0, pageSize: 25} as PageEvent),
        tap(page => {
          this.pageIndex = page.pageIndex;
          this.pageSize = page.pageSize;
          this.isLoading = true;
          this.loadError = undefined;
        }),
        switchMap(page => forkJoin({
          report: this.facilityViewService.getReportSummary(this.facilityId, this.submissionId),
          issues: this.facilityViewService.getCategoryIssuesPage(this.facilityId, this.submissionId,
            this.categoryId, page.pageIndex + 1, page.pageSize),
          counts: this.facilityViewService.getReportCategoryCounts(this.facilityId, this.submissionId),
          categories: this.validationService.getValidationCategories().pipe(catchError(() => of([])))
        }).pipe(catchError(() => {
          this.loadError = 'Unable to load validation issues for this category.';
          return of(null);
        })))
      ))
    ).subscribe(response => {
      this.isLoading = false;
      if (!response) return;
      this.reportSummary = response.report;
      this.reportIssues = response.issues.records;
      this.availableCount = response.issues.metadata.totalCount;
      const stored = response.counts.categories.find(category =>
        category.categoryId.toLowerCase() === this.categoryId.toLowerCase());
      const current = response.categories.find(category =>
        category.id.toLowerCase() === this.categoryId.toLowerCase());
      this.issueCount = stored?.issues ?? this.availableCount;
      this.category = {
        id: this.categoryId,
        title: stored?.title ?? current?.title ?? this.categoryId,
        acceptable: stored?.acceptable ?? current?.acceptable ?? false,
        guidance: current?.guidance ?? '',
        severity: current?.severity ?? ''
      };
    });
  }

  changePage(page: PageEvent): void {
    this.pageChanges.next(page);
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
