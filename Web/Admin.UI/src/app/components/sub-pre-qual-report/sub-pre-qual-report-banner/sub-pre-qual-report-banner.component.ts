import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'app-sub-pre-qual-report-banner',
  imports: [
    CommonModule,
  ],
  templateUrl: './sub-pre-qual-report-banner.component.html',
  styleUrls: ['./sub-pre-qual-report-banner.component.scss']
})
export class SubPreQualReportBannerComponent {
  submissionId: string = '362574';
  facilityName: string = 'University of Oklahoma - HSC';
}
