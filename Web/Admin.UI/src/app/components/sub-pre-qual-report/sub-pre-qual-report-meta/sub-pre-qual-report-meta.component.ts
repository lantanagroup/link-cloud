import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { Subscription } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IReportListSummary } from '../../tenant/facility-view/report-view.interface';

@Component({
  selector: 'app-sub-pre-qual-report-meta',
  imports: [
    CommonModule,
    VdIconComponent
  ],
  templateUrl: './sub-pre-qual-report-meta.component.html',
  styleUrls: ['./sub-pre-qual-report-meta.component.scss']
})
export class SubPreQualReportMetaComponent {
  private subscription: Subscription | undefined;

  facilityId: string = '';
  submissionId: string = '362574';
  status: string = 'Failed Submission';
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
    const map: Record<string, { icon: string; class: string }> = {
      'Successful Submission': {
        icon: 'success-status.svg',
        class: 'success',
      },
      'Submitted with Issues': {
        icon: 'warning-status.svg',
        class: 'warning',
      },
      'Failed Submission': {
        icon: 'failed-status.svg',
        class: 'error',
      },
      'Error Log': {
        icon: 'failed-status.svg',
        class: 'error',
      },
    };

    return map[this.status] || { icon: 'warning-status.svg', class: 'warning' };
  }
}
