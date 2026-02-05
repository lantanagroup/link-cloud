import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ValidationResultsComponent} from "../validation-results/validation-results.component";
import {CommonModule} from "@angular/common";

import {ActivatedRoute, Router, RouterLink, RouterLinkActive} from "@angular/router";
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary, IReportListSummary } from '../report-view.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { ViewMeasureReportComponent } from '../view-measure-report/view-measure-report.component';
import { forkJoin, Subscription } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faXmark,
  faRotate,
  faArrowLeft,
  faFileArrowDown,
  faFileInvoice,
  faSort,
  faSortUp,
  faSortDown,
  faGears
} from '@fortawesome/free-solid-svg-icons';
import { LoadingService } from 'src/app/services/loading.service';
import { DonutChartComponent } from 'src/app/components/core/donut-chart/donut-chart.component';
import { ViewReportTableCommandComponent } from './table-command/view-report-table-command.component';
import { AcquisitionLogService } from '../../acquisition-log/acquisition-log.service';
import { IDataAcquisitionLogStatistics } from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';
import { ReportAnalysisComponent } from './report-analysis/report-analysis.component';
import { IReportSchedule } from '../../../../interfaces/report/report-schedule.interface';
import { ReportService } from '../../../../services/gateway/report/report.service';
import { IReportEntry, IReportEntrySummary, ReportingStatus, SubmissionStatus } from '../../../../interfaces/report/report-entry.interface';

@Component({
  selector: 'app-view-report',
  imports: [
    CommonModule,
    FontAwesomeModule,
    FormsModule,
    MatDialogModule,
    MatToolbarModule,
    MatIconModule,
    MatPaginatorModule,
    MatTabsModule,
    MatButtonModule,
    MatTooltipModule,
    MatTableModule,
    MatSortModule,
    RouterLink,
    RouterLinkActive,
    ViewReportTableCommandComponent,
    ReportAnalysisComponent,
    DonutChartComponent
  ],
  templateUrl: './view-report.component.html',
  styleUrl: './view-report.component.scss'
})
export class ViewReportComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFileArrowDown = faFileArrowDown;
  faFileInvoice = faFileInvoice;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;
  faGears = faGears;

  facilityId: string = '';
  reportId: string = '';

  reportSummary: IReportSchedule | undefined;
  dataAcquisitionLogStatistics: IDataAcquisitionLogStatistics | undefined;

  // Add summary data for donut charts
  reportEntrySummary: IReportEntrySummary | undefined;
  measureIpCountsData: Record<string, number> = {};
  reportStatusData: Record<string, number> = {};
  validationStatusData: Record<string, number> = {};

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  measureReports: IMeasureReportSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  displayedColumns: string[] = ['id', 'measure', 'resourceCount', 'reportingStatus', 'submissionStatus', 'action'];
  dataSource = new MatTableDataSource<IReportEntry>([]);

  //filters
  patientFilter: string = '';
  reportFilter: string = '';
  selectedMeasureFilter: string = 'any';
  selectedReportStatusFilter: ReportingStatus|string = 'any';
  selectedValidationStatusFilter: SubmissionStatus|string = 'any';
  measures: string[] = [];
  reportStatuses: ReportingStatus[] = [
    ReportingStatus.PatientIdentified,
    ReportingStatus.NotReportable,
    ReportingStatus.PendingValidation,
    ReportingStatus.PassedValidation,
    ReportingStatus.FailedValidation
  ];
  validationStatuses: SubmissionStatus[] = [
    SubmissionStatus.PendingValidation,
    SubmissionStatus.Submitting,
    SubmissionStatus.Submitted,
    SubmissionStatus.FailedSubmission,
    SubmissionStatus.NotEligable
  ];

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private router: Router,
    private dialog: MatDialog,
    private facilityViewService: FacilityViewService,
    private acquisitionLogService: AcquisitionLogService,
    private loadingService: LoadingService,
    private reportService: ReportService) { }

  ngOnInit(): void {
    this.facilityId = this.route.snapshot.paramMap.get('facilityId') || '';
    this.reportId = this.route.snapshot.paramMap.get('reportId') || '';
    this.loadReportSchedule();
    this.loadReportEntries();
    this.loadReportEntrySummary();
  }

  ngOnDestroy(): void {

  }

  loadReportSchedule(): void {
    this.loadingService.show();
    this.reportService.getReportSchedule(this.reportId)
      .subscribe({
        next: (data) => {
          this.reportSummary = data;
          // Populate measures from report types
          if (data.reportTypes && data.reportTypes.length > 0) {
            this.measures = data.reportTypes;
          }
          this.loadingService.hide();
        },
        error: (error) => {
          console.error('Error loading report schedule:', error);
          this.loadingService.hide();
        }
      });
  }

  loadReportEntries(): void {
    this.loadingService.show();

    // Build filter parameters
    const patientId = this.patientFilter || undefined;
    const reportScheduleId = this.reportFilter || this.reportId;
    const reportingStatus = this.selectedReportStatusFilter !== 'any'
      ? this.selectedReportStatusFilter as ReportingStatus
      : undefined;
    const submissionStatus = this.selectedValidationStatusFilter !== 'any'
      ? this.selectedValidationStatusFilter as SubmissionStatus
      : undefined;
    const reportType = this.selectedMeasureFilter !== 'any'
      ? this.selectedMeasureFilter
      : undefined;

    this.reportService.searchReportEntries(
      this.facilityId,
      patientId,
      reportScheduleId,
      reportingStatus,
      submissionStatus,
      reportType,
      this.sortBy || undefined,
      this.sortOrder || undefined,
      this.paginationMetadata.pageSize || this.defaultPageSize,
      (this.paginationMetadata.pageNumber || this.defaultPageNumber) + 1 // API uses 1-based indexing
    ).subscribe({
      next: (data) => {
        this.dataSource.data = data.records;
        this.paginationMetadata = {
          pageSize: data.metadata.pageSize,
          pageNumber: data.metadata.pageNumber - 1, // Convert to 0-based for mat-paginator
          totalCount: data.metadata.totalCount,
          totalPages: data.metadata.totalPages
        };
        this.loadingService.hide();
      },
      error: (error) => {
        console.error('Error loading report entries:', error);
        this.loadingService.hide();
      }
    });
  }

  loadReportEntrySummary(): void {
    this.reportService.getReportEntrySummary(this.reportId)
      .subscribe({
        next: (data) => {
          this.reportEntrySummary = data;
          this.measureIpCountsData = data.reportTypeCounts;
          this.reportStatusData = data.reportingStatusCounts;
          this.validationStatusData = data.submissionStatusCounts;
        },
        error: (error) => {
          console.error('Error loading report entry summary:', error);
        }
      });
  }

  onSelectReport(measureReport: IReportEntry): void {

    const dialogConfig = new MatDialogConfig();
    dialogConfig.minWidth = '90vw';
    dialogConfig.maxHeight = '85vh';
    dialogConfig.panelClass = 'link-dialog-container';
    dialogConfig.data = {
      dialogTitle: 'Measure Report Details',
      viewOnly: false,
      facilityId: this.facilityId,
      measureReport: measureReport
    };

    this.dialog.open(ViewMeasureReportComponent, dialogConfig);
  }

  onDownload() {
    this.facilityViewService.downloadReport(this.facilityId, this.reportId);
  }

  clearFilters(): void {
    this.patientFilter = '';
    this.reportFilter = '';
    this.selectedMeasureFilter = 'any';
    this.selectedReportStatusFilter = 'any';
    this.selectedValidationStatusFilter = 'any';
    this.paginationMetadata.pageNumber = 0;
    this.sortBy = null;
    this.sortOrder = null;
    this.loadReportEntries();
  }

  onRefresh(): void {
    this.loadReportSchedule();
    this.loadReportEntries();
    this.loadReportEntrySummary();
  }

  onPatientIdChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onReportIdChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onReportStatusFilterChange(event: any): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onValidationStatusFilterChange(event: any): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onMeasureFilterChange(event: any): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadReportEntries();
  }

  onSortChange(sort: Sort): void {
    if (sort.direction) {
      this.sortBy = sort.active;
      this.sortOrder = sort.direction === 'asc' ? 'ascending' : 'descending';
    } else {
      this.sortBy = null;
      this.sortOrder = null;
    }
    this.loadReportEntries();
  }

  onViewAcquisitionLog() {

    const urlTree = this.router.createUrlTree(['/tenant/acquisition-log'], {
      queryParams: { reportId: this.reportId }
    });
    const fullUrl = this.router.serializeUrl(urlTree);

    window.open(fullUrl, '_blank');
    //this.router.navigate(['tenant/acquisition-log'], { queryParams: { reportId: this.reportId } });
  }

  onViewQueryAnalysis() {
    this.loadingService.show();
    this.acquisitionLogService.getAcquisitionLogStatistics(this.reportId).subscribe({
      next: (response: IDataAcquisitionLogStatistics) => {
        this.dataAcquisitionLogStatistics = response;
        this.loadingService.hide();
      },
      error: (error) => {
        console.error('Error loading acquisition log statistics:', error);
        this.loadingService.hide();
      }
    });
  }

  onTabSelected(event: any): void {
    if (event === 1) {
      this.onViewQueryAnalysis();
    }
  }

  getTotalResourceCount(measureReports: any[]): number {
    if (!measureReports || measureReports.length === 0) {
      return 0;
    }

    return measureReports.reduce((total, report) => {
      if (report.resourceCount) {
        const resourceCount = Object.values(report.resourceCount).reduce((sum: number, count) => sum + (count as number), 0);
        return total + resourceCount;
      }
      return total;
    }, 0);
  }

  getReportingStatusClass(status: ReportingStatus): string {
    switch (status) {
      case ReportingStatus.PatientIdentified:
        return 'status-processing';
      case ReportingStatus.NotReportable:
        return 'status-not-reportable';
      case ReportingStatus.PendingValidation:
        return 'status-pending';
      case ReportingStatus.PassedValidation:
        return 'status-passed';
      case ReportingStatus.FailedValidation:
        return 'status-failed';
      default:
        return '';
    }
  }

  getSubmissionStatusClass(status: SubmissionStatus): string {
    switch (status) {
      case SubmissionStatus.PendingValidation:
        return 'status-pending';
      case SubmissionStatus.Submitting:
        return 'status-processing';
      case SubmissionStatus.Submitted:
        return 'status-submitted';
      case SubmissionStatus.FailedSubmission:
        return 'status-failed';
      case SubmissionStatus.NotEligable:
        return 'status-not-reportable';
      default:
        return '';
    }
  }

  getReportingStatusText(status: ReportingStatus): string {
    switch (status) {
      case ReportingStatus.PatientIdentified:
        return 'Patient Identified';
      case ReportingStatus.NotReportable:
        return 'Not Reportable';
      case ReportingStatus.PendingValidation:
        return 'Pending Validation';
      case ReportingStatus.PassedValidation:
        return 'Passed Validation';
      case ReportingStatus.FailedValidation:
        return 'Failed Validation';
      default:
        return '';
    }
  }

  getSubmissionStatusText(status: SubmissionStatus): string {
    switch (status) {
      case SubmissionStatus.PendingValidation:
        return 'Pending Validation';
      case SubmissionStatus.Submitting:
        return 'Submitting';
      case SubmissionStatus.Submitted:
        return 'Submitted';
      case SubmissionStatus.FailedSubmission:
        return 'Failed Submission';
      case SubmissionStatus.NotEligable:
        return 'Not Eligible';
      default:
        return '';
    }
  }

  navBack(): void {
    this.location.back();
  }
}
