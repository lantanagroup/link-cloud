import { Component, OnDestroy, OnInit } from '@angular/core';

import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FacilityViewService } from '../tenant/facility-view/facility-view.service';
import { IValidationIssue } from '../tenant/facility-view/report-view.interface';
import { LinkAdminSubnavBarComponent } from "../core/link-admin-subnav-bar/link-admin-subnav-bar.component";
import { SubPreQualReportBannerComponent } from "./sub-pre-qual-report-banner/sub-pre-qual-report-banner.component";
import { SubPreQualReportCategoriesTableComponent } from './sub-pre-qual-report-categories-table/sub-pre-qual-report-categories-table.component';
import { SubPreQualReportIssuesTableComponent } from './sub-pre-qual-report-issues-table/sub-pre-qual-report-issues-table.component';
import { SubPreQualReportMetaComponent } from './sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import { SubPreQualReportSubnavComponent } from './sub-pre-qual-report-subnav/sub-pre-qual-report-subnav.component';
import { SubPreQualReportSummaryComponent } from './sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import { Subscription } from 'rxjs';

/**
 * Main component for the sub-pre-qual report view
 * Manages the overall layout and coordinates data between child components
 */
@Component({
  selector: 'app-sub-pre-qual-report',
  imports: [
    CommonModule,
    LinkAdminSubnavBarComponent,
    SubPreQualReportBannerComponent,
    SubPreQualReportSubnavComponent,
    SubPreQualReportMetaComponent,
    SubPreQualReportSummaryComponent,
    SubPreQualReportCategoriesTableComponent,
    SubPreQualReportIssuesTableComponent
  ],
  templateUrl: './sub-pre-qual-report.component.html',
  styleUrls: ['./sub-pre-qual-report.component.scss'],
  standalone: true
})
export class SubPreQualReportComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';

  // Counts for each type of issue
  unacceptableCount: number = 0;
  acceptableCount: number = 0;
  uncategorizedCount: number = 0;

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

  /**
   * Loads report data and calculates issue counts
   * Updates the counts for each type of issue
   */
  private loadReportData(): void {
    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (issues: IValidationIssue[]) => {
        // Reset counts
        this.unacceptableCount = 0;
        this.acceptableCount = 0;
        this.uncategorizedCount = 0;

        // Calculate counts
        issues.forEach(issue => {
          if (issue.categories.length === 0) {
            this.uncategorizedCount++;
          } else {
            // Check if all categories are acceptable
            const allAcceptable = issue.categories.every(cat => cat.acceptable);
            if (allAcceptable) {
              this.acceptableCount++;
            } else {
              this.unacceptableCount++;
            }
          }
        });
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
