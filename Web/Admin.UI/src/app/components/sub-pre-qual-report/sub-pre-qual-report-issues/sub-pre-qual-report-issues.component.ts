import { Component, OnDestroy, OnInit } from '@angular/core';

import { ActivatedRoute, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IValidationIssue } from '../../tenant/facility-view/report-view.interface';
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
  category: string = '';

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
      this.category = params['category'];
    });
  }


  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
