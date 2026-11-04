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
import {catchError, combineLatestWith, forkJoin, map, of, Subject, Subscription, switchMap, startWith, tap} from 'rxjs';
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
      tap({
        next: params => {
          this.facilityId = params['facilityId'];
          this.submissionId = params['submissionId'];
          this.categoryId = params['categoryId'];
          this.category = undefined;
          this.issueCount = 0;
          this.reportSummary = undefined;
          this.reportIssues = [];
          this.availableCount = 0;
        }
      }),
      switchMap(() => {
        const metadata = forkJoin({
          report: this.facilityViewService.getReportSummary(this.facilityId, this.submissionId),
          counts: this.facilityViewService.getReportCategoryCounts(this.facilityId, this.submissionId),
          categories: this.validationService.getValidationCategories().pipe(catchError(() => of([])))
        }).pipe(catchError(() => of(null)));
        const issues = this.pageChanges.pipe(
          startWith({pageIndex: 0, pageSize: 25} as PageEvent),
          tap({
            next: page => {
              this.pageIndex = page.pageIndex;
              this.pageSize = page.pageSize;
              this.isLoading = true;
              this.loadError = undefined;
            }
          }),
          switchMap(page => this.facilityViewService.getCategoryIssuesPage(
            this.facilityId, this.submissionId, this.categoryId, page.pageIndex + 1, page.pageSize
          ).pipe(catchError(() => of(null))))
        );
        // Metadata stays subscribed once per route while only findings change with each page.
        return metadata.pipe(
          combineLatestWith(issues),
          map(([metadata, issues]) => ({metadata, issues}))
        );
      })
    ).subscribe({
      next: response => {
        this.isLoading = false;
        if (!response.metadata || !response.issues) {
          this.loadError = 'Unable to load validation issues for this category.';
          return;
        }
        this.reportSummary = response.metadata.report;
        this.reportIssues = response.issues.records;
        this.availableCount = response.issues.metadata.totalCount;
        const stored = response.metadata.counts.categories.find(category =>
          category.categoryId.toLowerCase() === this.categoryId.toLowerCase());
        const current = response.metadata.categories.find(category =>
          category.id.toLowerCase() === this.categoryId.toLowerCase());
        this.issueCount = stored?.issues ?? this.availableCount;
        this.category = {
          id: this.categoryId,
          title: stored?.title ?? current?.title ?? this.categoryId,
          acceptable: stored?.acceptable ?? current?.acceptable ?? false,
          guidance: current?.guidance ?? '',
          severity: current?.severity ?? ''
        };
      }
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
