import { Component, OnDestroy, OnInit } from '@angular/core';

import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IValidationIssue, IValidationIssueCategorySummary, IReportListSummary, IValidationIssueCategory } from '../../tenant/facility-view/report-view.interface';
import { SubPreQualReportIssuesTableComponent } from '../sub-pre-qual-report-issues-table/sub-pre-qual-report-issues-table.component';
import { SubPreQualReportMetaComponent } from '../sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import { SubPreQualReportSummaryComponent } from '../sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import { Subscription } from 'rxjs';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-sub-pre-qual-report-issues',
  imports: [
    CommonModule,
    SubPreQualReportMetaComponent,
    SubPreQualReportSummaryComponent,
    SubPreQualReportIssuesTableComponent,
    MatTabsModule,
    RouterLink,
    RouterLinkActive,
    MatIconModule,
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

  category: IValidationIssueCategory | undefined;
  reportIssues: IValidationIssue[] | undefined;
  reportIssuesSummary: IValidationIssueCategorySummary[] | undefined;
  reportSummary: IReportListSummary | undefined;
  

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit() {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
      this.categoryId = params['categoryId'];
      this.loadReportData();
    });
  }

  private loadReportData(): void {
    this.facilityViewService.getReportSummary(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.reportSummary = response;
      },
      error: (error) => {
        console.error('Error getting report summary:', error);
      }
    });

    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.reportIssues = response;

        if (this.reportIssues && this.reportIssues.length > 0) {
          // Find all issues that belong to this category
          const categoryIssues = this.reportIssues.filter(issue =>
            issue.categories.some(cat =>
              cat.id === this.categoryId
            ) || (!issue.categories && this.categoryId === 'Uncategorized')
          );

          // Get the first category that matches to get guidance
          this.category = categoryIssues[0].categories[0];

          this.facilityViewService.getReportIssuesSummary(this.reportIssues).subscribe({
            next: (response) => {
              this.reportIssuesSummary = response;
            },
            error: (error) => {
              console.error('Error getting report issues summary:', error);
            }
          });
        } else {
          // No issues found
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
