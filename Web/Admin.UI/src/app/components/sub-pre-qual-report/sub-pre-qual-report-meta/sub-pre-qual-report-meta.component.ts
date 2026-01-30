import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import {
  IReportListSummary,
  IValidationIssueCategory,
  ScheduleStatus
} from '../../tenant/facility-view/report-view.interface';

import { VdIconComponent } from '../../core/vd-icon/vd-icon.component';
import {DatePipe} from "@angular/common";

@Component({
  selector: 'app-sub-pre-qual-report-meta',
  standalone: true,
  imports: [VdIconComponent, DatePipe],
  templateUrl: './sub-pre-qual-report-meta.component.html',
  styleUrls: ['./sub-pre-qual-report-meta.component.scss'],
})
export class SubPreQualReportMetaComponent implements OnInit, OnChanges {

  @Input() category?: IValidationIssueCategory;
  @Input() reportSummary?: IReportListSummary;

  submissionId = '';

  status: ScheduleStatus = ScheduleStatus.New; // default value
  statusMeta = { icon: '', label: '', class: '' }; // initialize to avoid undefined

  reportStartDate!: Date;
  reportEndDate!: Date;
  timestamp!: Date;
  fileSize = 'XXMB';

  private readonly STATUS_META: Record<ScheduleStatus, {
    icon: string;
    label: string;
    class: string;
  }> = {
    [ScheduleStatus.New]: {
      icon: 'failed-status.svg',
      label: 'Not Submitted',
      class: 'error',
    },
    [ScheduleStatus.Scheduled]: {
      icon: 'scheduled-status.svg',
      label: 'Scheduled',
      class: 'info',
    },
    [ScheduleStatus.EndOfPeriod]: {
      icon: 'end-period-status.svg',
      label: 'End of Period',
      class: 'warning',
    },
    [ScheduleStatus.Submitted]: {
      icon: 'success-status.svg',
      label: 'Submitted',
      class: 'success',
    }
  };

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.submissionId =
      this.route.snapshot.paramMap.get('submissionId') ?? '';
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['reportSummary'] && this.reportSummary) {
      // ensure status is a valid ScheduleStatus
      this.status = this.reportSummary.status ?? ScheduleStatus.New;
      this.statusMeta = this.STATUS_META[this.status] ?? this.STATUS_META[ScheduleStatus.New];

      this.reportStartDate = this.reportSummary.reportStartDate;
      this.reportEndDate = this.reportSummary.reportEndDate;
      this.timestamp = this.reportSummary.submitReportDateTime;
    }
  }
}
