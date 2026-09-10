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
import {forkJoin, Subject, Subscription} from 'rxjs';
import {debounceTime, distinctUntilChanged, take} from 'rxjs/operators';
import {ResubmitDialogComponent} from "./resubmit-dialog.component";
import {AggregationService} from '../../../services/gateway/aggregation/aggregation.service';
import {DeleteConfirmationDialogComponent} from '../../core/delete-confirmation-dialog/delete-confirmation-dialog.component';
import {AlertDialogComponent} from '../../core/alert-dialog/alert-dialog.component';
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
import {MatTabsModule} from '@angular/material/tabs';
import {LocationsListComponent} from './locations-list/locations-list.component';
import {EncountersListComponent} from './encounters-list/encounters-list.component';
import {FacilityReportingPlansComponent} from './facility-reporting-plans/facility-reporting-plans.component';
import {AppConfigService} from '../../../services/app-config.service';

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
    MatTooltipModule,
    MatTabsModule,
    LocationsListComponent,
    EncountersListComponent,
    FacilityReportingPlansComponent
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.scss'
})
export class FacilityViewComponent implements OnInit, OnDestroy {
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  private subscription: Subscription | undefined;
  private reportIdSubscription: Subscription | undefined;
  private reportIdSubject = new Subject<string>();
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
  reportIdFilter: string = '';
  statusFilters: string[] = [];
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
    private aggregationService: AggregationService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private appConfigService: AppConfigService) {
  }

  /**
   * DMRP feature flag, mirroring the nav bar's gating: the Reporting Plans tab only exists while
   * the module is on, because its api/dmrp routes do not exist when it is off.
   */
  get dmrpEnabled(): boolean {
    return this.appConfigService.config?.dmrpEnabled ?? false;
  }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.paginationMetadata.totalCount = 0;
    this.paginationMetadata.totalPages = 0;

    this.reportIdSubscription = this.reportIdSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(() => {
      this.paginationMetadata.pageNumber = 0;
      this.loadReportSchedules();
    });

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
    this.reportIdSubscription?.unsubscribe();
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
    if (this.refreshTimeoutId !== null) {
      clearTimeout(this.refreshTimeoutId);
    }
  }

  getColumns(): string[] {
    const cols = ['id', 'facilityId', 'reportStartDate', 'createDate', 'frequency', 'reportTypes', 'patientsInCensus', 'patientsInIP', 'status', 'action', 'delete'];
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
      this.statusFilters.length > 0 ? this.statusFilters : undefined,
      undefined,
      this.showDeleted,
      this.currentSortBy,
      this.currentSortOrder,
      Math.max(1, this.paginationMetadata.pageSize || this.defaultPageSize),
      this.paginationMetadata.pageNumber + 1, // API expects 1-based indexing
      this.createDateFilter ?? undefined,
      this.reportIdFilter || undefined
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

  onReportIdInput(value: string): void {
    this.reportIdFilter = value;
    this.reportIdSubject.next(value);
  }

  applyFilters(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  clearFilters(): void {
    this.reportIdFilter = '';
    this.statusFilters = [];
    this.frequencyFilter = '';
    this.reportStartDateFilter = null;
    this.reportEndDateFilter = null;
    this.createDateFilter = null;
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  hasActiveFilters(): boolean {
    return !!(this.reportIdFilter || this.statusFilters.length > 0 || this.frequencyFilter ||
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

  onAbortReport(reportScheduleId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '480px',
      data: {
        title: 'Abort In-Progress Report',
        message: 'Stop this in-progress report? Queued acquisition and normalization work for this report will be dropped. Other reports and census jobs for the facility are not affected. The report will be soft-deleted and can be restored later.',
        icon: 'cancel',
        iconColor: 'warn',
        confirmButtonText: 'Abort report'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(confirmed => {
      if (!confirmed) return;

      const progressSnackBar = this.snackBar.open('Aborting report, please wait...', 'Close');

      this.aggregationService.abortReport(reportScheduleId).subscribe({
        next: () => {
          progressSnackBar.dismiss();
          this.snackBar.open('Report aborted. In-flight work for this report will stop.', 'Close', { duration: 3000, panelClass: 'success-snackbar' });
          this.paginationMetadata.pageNumber = 0;
          this.loadReportSchedules();
        },
        error: (err) => {
          progressSnackBar.dismiss();
          const detail = this.extractDetail(err);
          this.dialog.open(AlertDialogComponent, {
            width: '420px',
            data: {
              title: 'Abort Failed',
              message: detail || 'Failed to abort the report. Please try again.',
              icon: 'error',
              iconColor: 'warn'
            }
          });
        }
      });
    });
  }

  onSoftDeleteReport(reportScheduleId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: 'Are you sure you want to soft delete this report and all its associated acquisition logs?'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(confirmed => {
      if (!confirmed) return;

      const progressSnackBar = this.snackBar.open('Soft deleting report, please wait...', 'Close');

      this.aggregationService.softDeleteReport(reportScheduleId).subscribe({
        next: () => {
          progressSnackBar.dismiss();
          this.snackBar.open('Report soft deleted successfully', 'Close', { duration: 3000, panelClass: 'success-snackbar' });
          this.paginationMetadata.pageNumber = 0;
          this.loadReportSchedules();
        },
        error: (err) => {
          progressSnackBar.dismiss();
          const detail = this.extractDetail(err);
          const is409 = err.status === 409;
          this.dialog.open(AlertDialogComponent, {
            width: '420px',
            data: {
              title: is409 ? 'Report In Progress' : 'Soft Delete Failed',
              message: detail || (is409
                ? 'This report cannot be deleted because it is currently in progress. Please wait for it to complete.'
                : 'Failed to soft delete the report. Please try again.'),
              icon: is409 ? 'running_with_errors' : 'error',
              iconColor: 'warn'
            }
          });
        }
      });
    });
  }

  onRestoreReport(reportScheduleId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Restore Report',
        message: 'Are you sure you want to restore this report and all its associated acquisition logs?',
        icon: 'restore',
        iconColor: 'primary',
        confirmButtonText: 'Restore'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(confirmed => {
      if (!confirmed) return;

      const progressSnackBar = this.snackBar.open('Restoring report, please wait...', 'Close');

      this.aggregationService.restoreReport(reportScheduleId).subscribe({
        next: () => {
          progressSnackBar.dismiss();
          this.snackBar.open('Report restored successfully', 'Close', { duration: 3000, panelClass: 'success-snackbar' });
          this.paginationMetadata.pageNumber = 0;
          this.loadReportSchedules();
        },
        error: (err) => {
          progressSnackBar.dismiss();
          const detail = this.extractDetail(err);
          this.dialog.open(AlertDialogComponent, {
            width: '420px',
            data: {
              title: 'Restore Failed',
              message: detail || 'Failed to restore the report. Please try again.',
              icon: 'error',
              iconColor: 'warn'
            }
          });
        }
      });
    });
  }

  private extractDetail(err: any): string | null {
    if (!err.error) return null;
    if (typeof err.error === 'object') return err.error.detail ?? null;
    try { return JSON.parse(err.error)?.detail ?? null; } catch { return null; }
  }

  onRefresh() {
    this.loadReportSchedules();
  }
}
