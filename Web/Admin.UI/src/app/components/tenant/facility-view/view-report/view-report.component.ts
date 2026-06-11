import { Component, OnInit } from '@angular/core';
import { CommonModule, Location } from '@angular/common';

import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from "@angular/router";
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary } from '../report-view.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { ViewMeasureReportComponent } from '../view-measure-report/view-measure-report.component';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faArrowLeft,
  faFileArrowDown,
  faFileInvoice,
  faFileLines,
  faGears,
  faRotate,
  faSort,
  faSortDown,
  faSortUp,
  faXmark
} from '@fortawesome/free-solid-svg-icons';
import { LoadingService } from 'src/app/services/loading.service';
import { ChartColorService } from 'src/app/services/chart-color.service';
import { DonutChartComponent } from 'src/app/components/core/donut-chart/donut-chart.component';
import { ViewReportTableCommandComponent } from './table-command/view-report-table-command.component';
import { AcquisitionLogService } from '../../acquisition-log/acquisition-log.service';
import {
  IDataAcquisitionLogStatistics
} from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';
import { ReportAnalysisComponent } from './report-analysis/report-analysis.component';
import { IReportSchedule } from '../../../../interfaces/report/report-schedule.interface';
import { ReportService } from '../../../../services/gateway/report/report.service';
import {
  IReportEntry,
  IReportEntrySummary,
  ReportingStatus,
  SubmissionStatus
} from '../../../../interfaces/report/report-entry.interface';

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
    MatSelectModule,
    MatFormFieldModule,
    MatInputModule,
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
  faFileLines = faFileLines;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;
  faGears = faGears;

  private readonly PAGE_SIZE_KEY = 'viewReportPageSize';
  facilityId: string = '';
  reportId: string = '';

  reportSummary: IReportSchedule | undefined;
  dataAcquisitionLogStatistics: IDataAcquisitionLogStatistics | undefined;

  // Add summary data for donut charts
  reportEntrySummary: IReportEntrySummary | undefined;
  measureIpCountsData: Record<string, number> = {};
  // Stable colors per report type, persisted across refreshes via ChartColorService.
  measureIpColors: Record<string, string> = {};
  reportStatusData: Record<string, number> = {};
  submissionStatusData: Record<string, number> = {};

  // Fixed donut-chart colors keyed by the labels produced by
  // getReportingStatusText / getSubmissionStatusText. Labels not listed
  // fall back to the chart's default palette.
  reportStatusColors: Record<string, string> = {
    'Patient Identified': '#1f77b4', // blue - in pipeline
    'Pending Validation': '#ff9800', // amber - in progress
    'Passed Validation': '#2ca02c',  // green - good
    'Failed Validation': '#d62728',  // red - bad
    'Not Reportable': '#9e9e9e',     // grey - neutral
  };
  submissionStatusColors: Record<string, string> = {
    'Pending Validation': '#ff9800', // amber - in progress
    'Submitting': '#1f77b4',         // blue - in progress
    'Submitted': '#2ca02c',          // green - good
    'Failed Submission': '#d62728',  // red - bad
    'Not Eligible': '#9e9e9e',       // grey - neutral
    'Pending': '#bdbdbd',            // light grey - neutral
  };

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  measureReports: IMeasureReportSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  displayedColumns: string[] = ['id', 'measure', 'resourceCount', 'reportingStatus', 'submissionStatus', 'submitReportDateTime', 'action'];
  dataSource = new MatTableDataSource<IReportEntry>([]);

  //filters
  patientFilter: string = '';
  reportFilter: string = '';
  selectedMeasureFilter: string = 'any';
  selectedReportStatusFilter: ReportingStatus[] = [];
  selectedSubmissionStatusFilters: Array<SubmissionStatus | 'pending'> = [];
  measures: string[] = [];
  reportStatuses: ReportingStatus[] = [
    ReportingStatus.PatientIdentified,
    ReportingStatus.NotReportable,
    ReportingStatus.PendingValidation,
    ReportingStatus.PassedValidation,
    ReportingStatus.FailedValidation
  ];
  submissionStatuses: Array<SubmissionStatus | 'pending'> = [
    'pending',
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
    private reportService: ReportService,
    private chartColorService: ChartColorService) { }

  ngOnInit(): void {
    const savedPageSize = localStorage.getItem(this.PAGE_SIZE_KEY);
    if (savedPageSize) {
      this.defaultPageSize = +savedPageSize;
    }
    this.paginationMetadata.pageSize = this.defaultPageSize;

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
    const reportingStatuses = this.selectedReportStatusFilter.length > 0
      ? this.selectedReportStatusFilter
      : undefined;

    const submissionStatusIsNull = this.selectedSubmissionStatusFilters.includes('pending');
    const submissionStatuses = this.selectedSubmissionStatusFilters
      .filter(s => s !== 'pending') as SubmissionStatus[];

    const reportType = this.selectedMeasureFilter !== 'any'
      ? this.selectedMeasureFilter
      : undefined;

    this.reportService.searchReportEntries(
      this.facilityId,
      patientId,
      reportScheduleId,
      reportingStatuses,
      submissionStatuses.length > 0 ? submissionStatuses : undefined,
      submissionStatusIsNull,
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
          this.measureIpColors = this.chartColorService.getColorMap(
            'measure-report-type',
            Object.keys(data.reportTypeCounts)
          );
          this.reportStatusData = Object.fromEntries(
            Object.entries(data.reportingStatusCounts)
              .map(([statusKey, count]) => {
                const statusValue = this.toReportingStatus(statusKey);
                const label = statusValue !== null
                  ? this.getReportingStatusText(statusValue)
                  : statusKey;

                return [label, count] as const;
              })
              .sort(([labelA], [labelB]) => labelA.localeCompare(labelB))
          ) as Record<string, number>;

          this.submissionStatusData = Object.fromEntries(
            Object.entries(data.submissionStatusCounts)
              .map(([statusKey, count]) => {
                const statusValue = this.toSubmissionStatus(statusKey);
                const label = statusValue !== null
                  ? this.getSubmissionStatusText(statusValue)
                  : statusKey;

                return [label, count] as const;
              })
              .sort(([labelA], [labelB]) => labelA.localeCompare(labelB))
          ) as Record<string, number>;
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
    this.selectedReportStatusFilter = [];
    this.selectedSubmissionStatusFilters = [];
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

  onSubmissionStatusFilterChange(event: any): void {
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
    localStorage.setItem(this.PAGE_SIZE_KEY, event.pageSize.toString());
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

  onReportStatusSliceSelected(label: string): void {
    const status = this.findReportingStatusByLabel(label);
    if (status === null) {
      return;
    }

    this.selectedReportStatusFilter = [status];
    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
  }

  onSubmissionStatusSliceSelected(label: string): void {
    if (this.isPendingSubmissionLabel(label)) {
      this.selectedSubmissionStatusFilters = ['pending'];
    } else {
      const status = this.findSubmissionStatusByLabel(label);
      if (status === null) {
        return;
      }
      this.selectedSubmissionStatusFilters = [status];
    }

    this.paginationMetadata.pageNumber = 0;
    this.loadReportEntries();
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
      case SubmissionStatus.Submitting:
        return 'status-processing';
      case SubmissionStatus.Submitted:
        return 'status-submitted';
      case SubmissionStatus.FailedSubmission:
        return 'status-failed';
      case SubmissionStatus.NotEligable:
        return 'status-not-reportable';
      default:
        return 'status-pending';
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
        return 'Pending';
    }
  }

  getSubmissionStatusFilterText(status: SubmissionStatus | 'pending'): string {
    if (status === 'pending') {
      return 'Pending';
    }

    return this.getSubmissionStatusText(status);
  }

  private findReportingStatusByLabel(label: string): ReportingStatus | null {
    const match = this.reportStatuses.find((status) => this.getReportingStatusText(status) === label);
    if (match !== undefined) {
      return match;
    }

    return this.toReportingStatus(label);
  }

  private findSubmissionStatusByLabel(label: string): SubmissionStatus | null {
    const match = this.submissionStatuses.find((status) =>
      status !== 'pending' && this.getSubmissionStatusText(status as SubmissionStatus) === label);
    if (match !== undefined && match !== 'pending') {
      return match;
    }

    return this.toSubmissionStatus(label);
  }

  private isPendingSubmissionLabel(label: string): boolean {
    return label.trim().toLowerCase() === 'pending';
  }

  private toReportingStatus(statusKey: string): ReportingStatus | null {
    const numericValue = Number(statusKey);
    if (!Number.isNaN(numericValue)) {
      return numericValue as ReportingStatus;
    }

    const enumValue = ReportingStatus[statusKey as keyof typeof ReportingStatus];
    return typeof enumValue === 'number' ? (enumValue as ReportingStatus) : null;
  }

  private toSubmissionStatus(statusKey: string): SubmissionStatus | null {
    const numericValue = Number(statusKey);
    if (!Number.isNaN(numericValue)) {
      return numericValue as SubmissionStatus;
    }

    const enumValue = SubmissionStatus[statusKey as keyof typeof SubmissionStatus];
    return typeof enumValue === 'number' ? (enumValue as SubmissionStatus) : null;
  }

  navBack(): void {
    this.location.back();
  }
}
