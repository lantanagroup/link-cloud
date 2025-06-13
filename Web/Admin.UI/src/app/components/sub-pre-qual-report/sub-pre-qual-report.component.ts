import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { LinkAdminSubnavBarComponent } from "../core/link-admin-subnav-bar/link-admin-subnav-bar.component";
import { SubPreQualReportBannerComponent } from "./sub-pre-qual-report-banner/sub-pre-qual-report-banner.component";
import { SubPreQualReportCategoriesTableComponent } from './sub-pre-qual-report-categories-table/sub-pre-qual-report-categories-table.component';
import { SubPreQualReportMetaComponent } from './sub-pre-qual-report-meta/sub-pre-qual-report-meta.component';
import { SubPreQualReportSubnavComponent } from './sub-pre-qual-report-subnav/sub-pre-qual-report-subnav.component';
import { SubPreQualReportSummaryComponent } from './sub-pre-qual-report-summary/sub-pre-qual-report-summary.component';
import { SubPreQualReportIssuesTableComponent } from './sub-pre-qual-report-issues-table/sub-pre-qual-report-issues-table.component';

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
export class SubPreQualReportComponent {

}
