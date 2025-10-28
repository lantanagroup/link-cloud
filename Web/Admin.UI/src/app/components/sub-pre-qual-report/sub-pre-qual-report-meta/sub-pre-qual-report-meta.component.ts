import { Component, OnDestroy, OnInit } from '@angular/core';

import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IReportListSummary } from '../../tenant/facility-view/report-view.interface';
import { Subscription } from 'rxjs';
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";

@Component({
  selector: 'app-sub-pre-qual-report-meta',
  imports: [
    CommonModule,
    VdIconComponent
  ],
  templateUrl: './sub-pre-qual-report-meta.component.html',
  styleUrls: ['./sub-pre-qual-report-meta.component.scss'],
  standalone: true
})
export class SubPreQualReportMetaComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;

  facilityId: string = '';
  submissionId: string = '';
  status: boolean = false;
  statusLabel: string = '';
  reportingPeriodStartDate: Date = new Date();
  reportingPeriodEndDate: Date = new Date();
  timestamp: Date = new Date();
  fileSize: string = 'XXMB';

  reportSummary: IReportListSummary | undefined;

  constructor(
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
    })

    this.facilityViewService.getReportSummary(this.facilityId, this.submissionId).subscribe({
      next: (response) => {
        this.reportSummary = response;
        this.status = this.reportSummary.submitted;
        this.reportingPeriodStartDate = this.reportSummary.reportStartDate;
        this.reportingPeriodEndDate = this.reportSummary.reportEndDate;
        this.timestamp = this.reportSummary.submitDate;
      }
    })
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  get statusMeta() {
    const map: Record<'true' | 'false', { icon: string; label: string; class: string }> = {
      true: {
        icon: 'success-status.svg',
        label: 'Submitted',
        class: 'success',
      },
      false: {
        icon: 'failed-status.svg',
        label: 'Not submitted',
        class: 'error',
      }
    };

    return map[String(this.status) as 'true' | 'false'];
  }
}
