import { Directive, OnDestroy, ViewChild, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSort, Sort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { faRotate } from '@fortawesome/free-solid-svg-icons';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged, take } from 'rxjs/operators';

import { AlertDialogComponent } from '../core/alert-dialog/alert-dialog.component';
import { DeleteConfirmationDialogComponent } from '../core/delete-confirmation-dialog/delete-confirmation-dialog.component';
import { ResubmitDialogComponent } from '../tenant/facility-view/resubmit-dialog.component';
import { IReportSchedule } from '../../interfaces/report/report-schedule.interface';
import {
  SCHEDULE_STATUS_OPTIONS,
  scheduleStatusBadgeClass,
  scheduleStatusLabel
} from '../../interfaces/report/schedule-status';
import { PaginationMetadata } from '../../models/pagination-metadata.model';
import { AggregationService } from '../../services/gateway/aggregation/aggregation.service';
import { ReportService } from '../../services/gateway/report/report.service';
import { TenantService } from '../../services/gateway/tenant/tenant.service';
import { LoadingService } from '../../services/loading.service';

/**
 * The report schedule grid shared by the reports dashboard and the facility view.
 *
 * The two pages show the same rows with the same filters, sorting, paging and row actions;
 * they differ only in which facility they scope to and whether the page size is remembered.
 * Everything else lived twice and had begun to drift -- the dashboard's sort map knew about
 * IsDeleted and the facility view's did not, and its soft-delete handler took a status
 * argument it never used.
 *
 * Declared @Directive() because an abstract class using Angular features (ViewChild,
 * lifecycle hooks, inject) needs Angular metadata even though it is never instantiated.
 */
@Directive()
export abstract class ReportScheduleGridBase implements OnDestroy {
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  protected readonly reportService = inject(ReportService);
  protected readonly loadingService = inject(LoadingService);
  protected readonly aggregationService = inject(AggregationService);
  protected readonly tenantService = inject(TenantService);
  protected readonly dialog = inject(MatDialog);
  protected readonly snackBar = inject(MatSnackBar);

  faRotate = faRotate;

  defaultPageNumber = 0;
  defaultPageSize = 10;
  paginationMetadata: PaginationMetadata = new PaginationMetadata();

  dataSource = new MatTableDataSource<IReportSchedule>([]);
  reportSchedules: IReportSchedule[] = [];
  highlightedRowIds = new Set<string>();
  protected pendingHighlight = false;

  showDeleted = false;

  // Filters
  reportIdFilter = '';
  statusFilters: string[] = [];
  frequencyFilter = '';
  reportStartDateFilter: Date | null = null;
  reportEndDateFilter: Date | null = null;
  createDateFilter: Date | null = null;

  readonly statusOptions = SCHEDULE_STATUS_OPTIONS;
  readonly statusLabel = scheduleStatusLabel;
  readonly statusBadgeClass = scheduleStatusBadgeClass;
  readonly frequencyOptions = ['Monthly', 'Weekly', 'Daily', 'Adhoc'];

  currentSortBy = 'CreateDate';
  currentSortOrder = 1; // 1 = Descending, 0 = Ascending

  protected reportIdSubject = new Subject<string>();
  protected reportIdSubscription: Subscription | undefined;
  protected subscription: Subscription | undefined;
  protected refreshTimeoutId: ReturnType<typeof setTimeout> | null = null;

  /** localStorage key for the remembered page size, or null to not remember it. */
  protected pageSizeStorageKey: string | null = null;

  /** The facility to scope the query to: fixed for a facility view, a filter on the dashboard. */
  protected abstract get facilityFilter(): string | undefined;

  ngOnDestroy(): void {
    this.reportIdSubscription?.unsubscribe();
    this.subscription?.unsubscribe();
    if (this.refreshTimeoutId !== null) {
      clearTimeout(this.refreshTimeoutId);
    }
  }

  /** Debounces report-id keystrokes into a reload. Call from ngOnInit. */
  protected watchReportIdFilter(): void {
    this.reportIdSubscription = this.reportIdSubject
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(() => {
        this.paginationMetadata.pageNumber = 0;
        this.loadReportSchedules();
      });
  }

  /** Restores the remembered page size and seeds the pagination metadata. Call from ngOnInit. */
  protected initPagination(): void {
    if (this.pageSizeStorageKey) {
      const saved = localStorage.getItem(this.pageSizeStorageKey);
      const parsed = saved ? +saved : 0;
      if (parsed > 0) {
        this.defaultPageSize = parsed;
      }
    }

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.paginationMetadata.totalCount = 0;
    this.paginationMetadata.totalPages = 0;
  }

  getColumns(): string[] {
    const cols = ['id', 'facilityId', 'reportStartDate', 'createDate', 'frequency', 'reportTypes',
      'patientsInCensus', 'patientsInIP', 'status', 'action', 'delete'];
    if (this.showDeleted) {
      cols.push('isDeleted');
    }
    return cols;
  }

  loadReportSchedules(): void {
    if (!this.pendingHighlight) {
      this.highlightedRowIds = new Set();
    }
    this.loadingService.isLoading.next(true);

    // The picker yields midnight, which would exclude everything later that day.
    const reportEndDateNormalized = this.reportEndDateFilter
      ? new Date(this.reportEndDateFilter.getFullYear(), this.reportEndDateFilter.getMonth(),
        this.reportEndDateFilter.getDate(), 23, 59, 59, 999)
      : undefined;

    this.reportService.searchReportSchedules(
      this.facilityFilter,
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

  onShowDeletedChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onRefresh(): void {
    this.loadReportSchedules();
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageNumber = event.pageIndex;
    if (event.pageSize > 0) {
      this.paginationMetadata.pageSize = event.pageSize;
      this.defaultPageSize = event.pageSize;
      if (this.pageSizeStorageKey) {
        localStorage.setItem(this.pageSizeStorageKey, event.pageSize.toString());
      }
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
        'status': 'Status',
        'isDeleted': 'IsDeleted'
      };

      this.currentSortBy = sortFieldMap[sort.active] || 'CreateDate';
      this.currentSortOrder = sort.direction === 'desc' ? 1 : 0;
    } else {
      this.currentSortBy = 'CreateDate';
      this.currentSortOrder = 1;
    }

    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onResubmit(reportId: string, facilityId: string): void {
    const dialogRef = this.dialog.open(ResubmitDialogComponent, {
      width: '420px',
      data: { facilityId, reportId }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (!result) {
        // user cancelled dialog
        return;
      }

      const { bypassSubmission, reportId: resubmitReportId } = result;

      this.tenantService.regenerateReport(facilityId, resubmitReportId, bypassSubmission)
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

  protected extractDetail(err: any): string | null {
    if (!err.error) return null;
    if (typeof err.error === 'object') return err.error.detail ?? null;
    try { return JSON.parse(err.error)?.detail ?? null; } catch { return null; }
  }
}
