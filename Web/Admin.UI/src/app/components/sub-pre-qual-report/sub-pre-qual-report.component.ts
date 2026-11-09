import {Component, OnDestroy, OnInit} from '@angular/core';

import {ActivatedRoute, RouterLink, RouterLinkActive} from '@angular/router';

import {FacilityViewService} from '../tenant/facility-view/facility-view.service';
import {
  IReportListSummary,
  IReportCategoryCount,
  IValidationIssueCategorySummary
} from '../tenant/facility-view/report-view.interface';
import {
  SubPreQualReportCategoriesTableComponent
} from './sub-pre-qual-report-categories-table/sub-pre-qual-report-categories-table.component';
import {SubPreQualReportMetaComponent} from './sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import {SubPreQualReportSummaryComponent} from './sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import {catchError, forkJoin, of, Subscription, switchMap, tap, throwError} from 'rxjs';
import {MatTabsModule} from '@angular/material/tabs';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';

/**
 * Main component for the sub-pre-qual report view
 * Manages the overall layout and coordinates data between child components
 */
@Component({
  selector: 'app-sub-pre-qual-report',
  imports: [
    SubPreQualReportMetaComponent,
    SubPreQualReportSummaryComponent,
    SubPreQualReportCategoriesTableComponent,
    MatTabsModule,
    RouterLink,
    RouterLinkActive,
    MatProgressSpinnerModule
],
  templateUrl: './sub-pre-qual-report.component.html',
  styleUrls: ['./sub-pre-qual-report.component.scss'],
  standalone: true
})
export class SubPreQualReportComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';
  categoryCounts: IReportCategoryCount[] = [];
  loadError: string | undefined;
  countsUnavailable = false;
  inProgress = false;
  reportIssuesSummary: IValidationIssueCategorySummary[] | undefined;
  reportSummary: IReportListSummary | undefined;
  isLoading: boolean = true;

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit() {
    this.subscription = this.route.params.pipe(
      tap({
        next: params => {
          this.facilityId = params['facilityId'];
          this.submissionId = params['submissionId'];
          this.isLoading = true;
          this.loadError = undefined;
          this.categoryCounts = [];
          this.reportIssuesSummary = undefined;
          this.countsUnavailable = false;
          this.inProgress = false;
          this.reportSummary = undefined;
        }
      }),
      switchMap(() => forkJoin({
        report: this.facilityViewService.getReportSummary(this.facilityId, this.submissionId, true),
        counts: this.facilityViewService.getReportCategoryCounts(this.submissionId).pipe(
          catchError(error => error.status === 404 ? of(null) : throwError(() => error))
        )
      }).pipe(
        catchError(() => {
          this.loadError = 'Unable to load prequalification category counts for this report.';
          return of(null);
        })
      ))
    ).subscribe(response => {
      this.isLoading = false;
      if (!response) {
        return;
      }
      this.reportSummary = response.report;
      this.countsUnavailable = response.counts === null;
      this.inProgress = response.counts?.inProgress ?? false;
      if (!response.counts) return;
      this.categoryCounts = response.counts.categories;
      this.reportIssuesSummary = this.categoryCounts.map(category => ({value: category.title, count: category.issues}));
    });
  }

  downloadReport(): void {
    this.facilityViewService.downloadValidationResults(this.facilityId, this.submissionId);
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
