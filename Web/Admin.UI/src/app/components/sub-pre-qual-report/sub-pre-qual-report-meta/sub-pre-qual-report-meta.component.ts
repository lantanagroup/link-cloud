import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";

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
  submissionId: string = '362574';
  status: string = 'Failed Submission';
  reportingPeriod: string = 'YYYY-MM-DD - YYYY-MM-DD';
  timestamp: string = '[Timestamp]';
  fileSize: string = 'XXMB';

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
