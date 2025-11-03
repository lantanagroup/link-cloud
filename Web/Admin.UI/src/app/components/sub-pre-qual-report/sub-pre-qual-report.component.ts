import { Component, OnDestroy, OnInit } from '@angular/core';

import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FacilityViewService } from '../tenant/facility-view/facility-view.service';
import { IValidationIssue, IValidationIssueCategorySummary, IReportListSummary } from '../tenant/facility-view/report-view.interface';
import { SubPreQualReportCategoriesTableComponent } from './sub-pre-qual-report-categories-table/sub-pre-qual-report-categories-table.component';
import { SubPreQualReportIssuesTableComponent } from './sub-pre-qual-report-issues-table/sub-pre-qual-report-issues-table.component';
import { SubPreQualReportMetaComponent } from './sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import { SubPreQualReportSummaryComponent } from './sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import { Subscription } from 'rxjs';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/**
 * Main component for the sub-pre-qual report view
 * Manages the overall layout and coordinates data between child components
 */
@Component({
  selector: 'app-sub-pre-qual-report',
  imports: [
    CommonModule,
    SubPreQualReportMetaComponent,
    SubPreQualReportSummaryComponent,
    SubPreQualReportCategoriesTableComponent,
    SubPreQualReportIssuesTableComponent,
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
  reportIssues: IValidationIssue[] | undefined;
  reportIssuesSummary: IValidationIssueCategorySummary[] | undefined;
  reportSummary: IReportListSummary | undefined;
  isLoading: boolean = true;

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit() {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
      this.loadReportData();
    });
  }

  updateIsLoading() {
    if (this.reportIssues != undefined && this.reportIssuesSummary != undefined && this.reportSummary != undefined) {
      this.isLoading = false;
    }
    else {
      this.isLoading = true;
    }
  }

  private loadReportData(): void {
    this.facilityViewService.getReportSummary(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.reportSummary = response;
        this.updateIsLoading();
      },
      error: (error) => {
        console.error('Error getting report summary:', error);
      }
    });

    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.reportIssues = response;
        this.updateIsLoading();
        if (this.reportIssues && this.reportIssues.length > 0) {
          this.facilityViewService.getReportIssuesSummary(this.reportIssues).subscribe({
            next: (response) => {
              this.reportIssuesSummary = response;
              this.updateIsLoading();
            },
            error: (error) => {
              console.error('Error getting report issues summary:', error);
            }
          });
        } else {
          // No issues found
          this.reportIssuesSummary = [];
          this.updateIsLoading();
        }
      },
      error: (error) => {
        console.error('Error getting report issues:', error);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
