import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {Location} from '@angular/common';
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {MatCardModule} from "@angular/material/card";
import {TenantService} from 'src/app/services/gateway/tenant/tenant.service';
import {IFacilityConfigModel} from 'src/app/interfaces/tenant/facility-config-model.interface';
import {FacilityViewService} from './facility-view.service';
import {IPagedReportListSummary, IReportListSummary} from './report-view.interface';
import {CommonModule} from '@angular/common';
import {PaginationMetadata} from 'src/app/models/pagination-metadata.model';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import {MatPaginatorModule, MatPaginator, PageEvent} from '@angular/material/paginator';
import {MatButtonModule} from '@angular/material/button';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {faRotate, faArrowLeft, faGears} from '@fortawesome/free-solid-svg-icons';
import {LoadingService} from 'src/app/services/loading.service';
import {forkJoin, Subscription} from 'rxjs';
import {ResubmitDialogComponent} from "./resubmit-dialog.component";
import {MatDialog} from "@angular/material/dialog";
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {IPagedReportSchedule, IReportSchedule} from "../../../interfaces/report/report-schedule.interface";
import {MatCell, MatCellDef} from "@angular/material/table";
import { FormsModule } from "@angular/forms";
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSortModule, MatSort, Sort } from '@angular/material/sort';
import { ReportService } from "../../../services/gateway/report/report.service";
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatTooltipModule} from '@angular/material/tooltip';

@Component({
  selector: 'app-facility-view',
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    RouterLink,
    MatCardModule,
    FormsModule,
    MatTableModule,
    MatSortModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckbox,
    MatTooltipModule
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.scss'
})
export class FacilityViewComponent implements OnInit, OnDestroy {
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  private subscription: Subscription | undefined;
  private refreshTimeoutId: ReturnType<typeof setTimeout> | null = null;

  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faGears = faGears;

  facilityId: string = '';
  facilityConfig: IFacilityConfigModel | undefined;
  scheduledReports: { cadence: string; measures: string[] }[] = []; // Array to hold scheduled reports

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  dataSource = new MatTableDataSource<IReportSchedule>([]);
  reportSchedules: IReportSchedule[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  highlightedRowIds = new Set<string>();
  private pendingHighlight = false;

  showDeleted: boolean = false;

  // Filters
  statusFilter: string = '';
  frequencyFilter: string = '';
  reportStartDateFilter: Date | null = null;
  reportEndDateFilter: Date | null = null;
  createDateFilter: Date | null = null;

  readonly statusOptions = ['New', 'Scheduled', 'EndOfPeriod', 'Submitted'];
  readonly frequencyOptions = ['Monthly', 'Weekly', 'Daily', 'Adhoc'];

  currentSortBy: string = 'CreateDate';
  currentSortOrder: number = 1; // 1 = Descending, 0 = Ascending

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private router: Router,
    private tenantService: TenantService,
    private facilityViewService: FacilityViewService,
    private loadingService: LoadingService,
    private reportService: ReportService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.paginationMetadata.totalCount = 0;
    this.paginationMetadata.totalPages = 0;

    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];

      this.loadingService.show();

      forkJoin([
        this.tenantService.getFacilityConfiguration(this.facilityId)
      ]).subscribe({
        next: (response) => {
          this.facilityConfig = response[0];

          this.scheduledReports = this.facilityConfig?.scheduledReports ? [
            {cadence: 'Daily', measures: this.facilityConfig.scheduledReports.daily},
            {cadence: 'Weekly', measures: this.facilityConfig.scheduledReports.weekly},
            {cadence: 'Monthly', measures: this.facilityConfig.scheduledReports.monthly}
          ] : [];

          this.loadReportSchedules();

          this.loadingService.hide();
        },
        error: (error) => {
          console.error('Error loading report summaries:', error);
          this.loadingService.hide();
        }
      });
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
    if (this.refreshTimeoutId !== null) {
      clearTimeout(this.refreshTimeoutId);
    }
  }

  getColumns(): string[] {
    const cols = ['id', 'facilityId', 'reportStartDate', 'createDate', 'frequency', 'reportTypes', 'patientsInCensus', 'patientsInIP', 'status', 'action'];
    if (this.showDeleted) cols.push('isDeleted');
    return cols;
  }

  loadFacilityConfig(): void {
    this.tenantService.getFacilityConfiguration(this.facilityId).subscribe({
      next: (response: IFacilityConfigModel) => {
        this.facilityConfig = response;

        this.scheduledReports = this.facilityConfig?.scheduledReports ? [
          {cadence: 'Daily', measures: this.facilityConfig.scheduledReports.daily},
          {cadence: 'Weekly', measures: this.facilityConfig.scheduledReports.weekly},
          {cadence: 'Monthly', measures: this.facilityConfig.scheduledReports.monthly}
        ] : []
      },
      error: (error) => {
        console.error('Error fetching facility configuration:', error);
      }
    });
  }

  loadReportSchedules(): void {
    if (!this.pendingHighlight) {
      this.highlightedRowIds = new Set();
    }
    this.loadingService.isLoading.next(true);
    const reportEndDateNormalized = this.reportEndDateFilter
      ? new Date(this.reportEndDateFilter.getFullYear(), this.reportEndDateFilter.getMonth(), this.reportEndDateFilter.getDate(), 23, 59, 59, 999)
      : undefined;
    this.reportService.searchReportSchedules(
      this.facilityId,
      this.frequencyFilter || undefined,
      undefined,
      this.reportStartDateFilter ?? undefined,
      reportEndDateNormalized,
      this.statusFilter || undefined,
      undefined,
      this.showDeleted,
      this.currentSortBy,
      this.currentSortOrder,
      Math.max(1, this.paginationMetadata.pageSize || this.defaultPageSize),
      this.paginationMetadata.pageNumber + 1, // API expects 1-based indexing
      this.createDateFilter ?? undefined
    ).subscribe({
      next: (data) => {
        this.reportSchedules = data.records;
        this.dataSource.data = this.reportSchedules;
        this.paginationMetadata = data.metadata;
        this.paginationMetadata.pageNumber = data.metadata.pageNumber - 1; // Convert back to 0-based
        this.loadingService.isLoading.next(false);
        if (this.pendingHighlight && data.records.length > 0) {
          this.pendingHighlight = false;
          this.highlightedRowIds = new Set([data.records[0].id]);
        }
      },
      error: (error) => {
        console.error('Error loading report schedules:', error);
        this.loadingService.isLoading.next(false);
      }
    });
  }

  applyFilters(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  clearFilters(): void {
    this.statusFilter = '';
    this.frequencyFilter = '';
    this.reportStartDateFilter = null;
    this.reportEndDateFilter = null;
    this.createDateFilter = null;
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  hasActiveFilters(): boolean {
    return !!(this.statusFilter || this.frequencyFilter ||
              this.reportStartDateFilter || this.reportEndDateFilter || this.createDateFilter);
  }

  onFacilityConfig(): void {
    this.router.navigate(['/tenant/facility', this.facilityId, 'edit']);
  }

  navBack(): void {
    this.location.back();
  }

  onShowDeletedChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageNumber = event.pageIndex;
    if (event.pageSize > 0) {
      this.paginationMetadata.pageSize = event.pageSize;
    }
    this.loadReportSchedules();
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction) {
      // Map UI column names to API field names
      const sortFieldMap: { [key: string]: string } = {
        'id': 'Id',
        'facilityId': 'FacilityId',
        'reportStartDate': 'ReportStartDate',
        'createDate': 'CreateDate',
        'frequency': 'Frequency',
        'status': 'Status'
      };

      this.currentSortBy = sortFieldMap[sort.active] || 'CreateDate';
      this.currentSortOrder = sort.direction === 'desc' ? 1 : 0;
    } else {
      // Reset to default sort
      this.currentSortBy = 'CreateDate';
      this.currentSortOrder = 1;
    }

    // Reset to first page when sorting changes
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onResubmit(reportId: string, facilityId: string): void {
    const dialogRef = this.dialog.open(ResubmitDialogComponent, {
      width: '420px',
      data: {
        facilityId: facilityId,
        reportId,
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (!result) {
        // user cancelled dialog
        return;
      }

      // result.bypassSubmission is true/false
      const { bypassSubmission, reportId } = result;

      // Call your service and pass bypass flag
      this.tenantService.regenerateReport(facilityId, reportId, bypassSubmission)
        .subscribe({
          next: () => {
            this.snackBar.open('Report resubmitted. The list will refresh in 3 seconds…', '', {
              duration: 3000,
              horizontalPosition: 'end',
              verticalPosition: 'top',
              panelClass: 'resubmit-snackbar'
            });
            this.refreshTimeoutId = setTimeout(() => {
              this.paginationMetadata.pageNumber = 0;
              this.currentSortBy = 'CreateDate';
              this.currentSortOrder = 1;
              this.sort?.sort({ id: '', start: 'asc', disableClear: false });
              this.pendingHighlight = true;
              this.loadReportSchedules();
            }, 3000);
          },
          error: err => {
            console.error('Resubmit failed', err);
            this.snackBar.open('Failed to resubmit report. Please try again.', '', {
              duration: 3500,
              horizontalPosition: 'end',
              verticalPosition: 'top',
              panelClass: 'error-snackbar'
            });
          }
        });
    });
  }

  onRefresh() {
    this.loadReportSchedules();
  }
}
