import { Component, OnDestroy, OnInit, Input, OnChanges, SimpleChanges } from '@angular/core';

import { ActivatedRoute } from '@angular/router';

import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IReportListSummary, IValidationIssueCategory, IValidationIssueCategorySummary } from '../../tenant/facility-view/report-view.interface';
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-sub-pre-qual-report-meta',
  imports: [
    VdIconComponent
  ],
  templateUrl: './sub-pre-qual-report-meta.component.html',
  styleUrls: ['./sub-pre-qual-report-meta.component.scss'],
  standalone: true
})
export class SubPreQualReportMetaComponent implements OnInit, OnDestroy, OnChanges {
  @Input() category: IValidationIssueCategory | undefined;
  @Input() reportSummary: IReportListSummary | undefined;
  private subscription: Subscription | undefined;
  submissionId: string = '';
  status: boolean = false;
  statusLabel: string = '';
  reportingPeriodStartDate: Date = new Date();
  reportingPeriodEndDate: Date = new Date();
  timestamp: Date = new Date();
  fileSize: string = 'XXMB';

  constructor(
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {
      this.submissionId = params['submissionId'];
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['reportSummary']) {
      if (!this.reportSummary) return;
      this.status = this.reportSummary.submitted;
      this.reportingPeriodStartDate = this.reportSummary.reportStartDate;
      this.reportingPeriodEndDate = this.reportSummary.reportEndDate;
      this.timestamp = this.reportSummary.submitDate;
    }
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
