import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {MatDialog, MatDialogModule} from '@angular/material/dialog';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatPaginator, MatPaginatorModule, PageEvent} from '@angular/material/paginator';
import {MatSort, MatSortModule, Sort} from '@angular/material/sort';
import {MatTooltipModule} from '@angular/material/tooltip';
import {BehaviorSubject, combineLatest, Observable, of, Subject, Subscription} from 'rxjs';
import {debounceTime, distinctUntilChanged, map, startWith, switchMap, take, tap} from 'rxjs/operators';
import {TenantService} from '../../../services/gateway/tenant/tenant.service';
import {LoadingService} from '../../../services/loading.service';
import {AggregationService} from '../../../services/gateway/aggregation/aggregation.service';
import {DeleteConfirmationDialogComponent} from '../../core/delete-confirmation-dialog/delete-confirmation-dialog.component';
import {AlertDialogComponent} from '../../core/alert-dialog/alert-dialog.component';
import {PaginationMetadata} from '../../../models/pagination-metadata.model';
import {CommonModule} from '@angular/common';
import {ResubmitDialogComponent} from "../../tenant/facility-view/resubmit-dialog.component";
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {faRotate} from '@fortawesome/free-solid-svg-icons';
import {IReportSchedule} from '../../../interfaces/report/report-schedule.interface';
import {ReportService} from '../../../services/gateway/report/report.service';
import {FormControl, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {MatCheckbox} from "@angular/material/checkbox";
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatAutocompleteModule} from '@angular/material/autocomplete';

@Component({
  selector: 'app-reports-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatPaginatorModule,
    MatTableModule,
    MatSortModule,
    MatTooltipModule,
    RouterLink,
    FontAwesomeModule,
    FormsModule,
    ReactiveFormsModule,
    MatCheckbox,
    MatSnackBarModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatAutocompleteModule
  ],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent implements OnInit, OnDestroy {
  @ViewChild(MatPaginator, { static: false }) paginator!: MatPaginator;
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  private subscription: Subscription | undefined;
  private reportIdSubscription: Subscription | undefined;
  private reportIdSubject = new Subject<string>();
  private readonly PAGE_SIZE_KEY = 'reportsDashboardPageSize';
  private refreshTimeoutId: ReturnType<typeof setTimeout> | null = null;
  defaultPageNumber: number = 0;
  defaultPageSize: number = 10;
  paginationMetadata: PaginationMetadata = new PaginationMetadata();
  faRotate = faRotate;
  showDeleted = false;

  dataSource = new MatTableDataSource<IReportSchedule>([]);
  reportSchedules: IReportSchedule[] = [];
  highlightedRowIds = new Set<string>();
  private pendingHighlight = false;

  currentSortBy: string = 'CreateDate';
  currentSortOrder: number = 1; // 1 = Descending, 0 = Ascending

  // Filters
  facilityInputControl = new FormControl<string>('');
  selectedFacilityId: string | null = null;
  filteredFacilities: Observable<{ facilityId: string; facilityName: string }[]> = of([]);
  private showDeletedSubject = new BehaviorSubject<boolean>(false);
  reportIdFilter: string = '';
  statusFilters: string[] = [];
  frequencyFilter: string = '';
  reportStartDateFilter: Date | null = null;
  reportEndDateFilter: Date | null = null;
  createDateFilter: Date | null = null;

  readonly statusOptions = ['New', 'Scheduled', 'EndOfPeriod', 'Submitted'];
  readonly frequencyOptions = ['Monthly', 'Weekly', 'Daily', 'Adhoc'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private reportService: ReportService,
    private loadingService: LoadingService,
    private dialog: MatDialog,
    private tenantService: TenantService,
    private aggregationService: AggregationService,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    const savedPageSize = localStorage.getItem(this.PAGE_SIZE_KEY);
    if (savedPageSize) {
      const parsed = +savedPageSize;
      if (parsed > 0) this.defaultPageSize = parsed;
    }

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.paginationMetadata.totalCount = 0;
    this.paginationMetadata.totalPages = 0;

    this.filteredFacilities = combineLatest([
      this.facilityInputControl.valueChanges.pipe(
        startWith(''),
        debounceTime(300),
        distinctUntilChanged(),
        // Clear the selected ID whenever the user edits the text. emitEvent:false
        // (used in onFacilitySelected) bypasses valueChanges, so the tap only
        // fires on real keystrokes, not on programmatic selection.
        tap(() => { this.selectedFacilityId = null; })
      ),
      this.showDeletedSubject
    ]).pipe(
      switchMap(([term, includeDeleted]) => {
        const search = typeof term === 'string' ? term : '';
        return this.tenantService.autocompleteFacilities(search, includeDeleted);
      }),
      map(results => Object.entries(results || {}).map(([facilityId, facilityName]) => ({ facilityId, facilityName: facilityName as string })))
    );

    this.reportIdSubscription = this.reportIdSubject.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(() => {
      this.paginationMetadata.pageNumber = 0;
      this.loadReportSchedules();
    });

    this.loadReportSchedules();
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

  loadReportSchedules(): void {
    if (!this.pendingHighlight) {
      this.highlightedRowIds = new Set();
    }
    this.loadingService.isLoading.next(true);
    const reportEndDateNormalized = this.reportEndDateFilter
      ? new Date(this.reportEndDateFilter.getFullYear(), this.reportEndDateFilter.getMonth(), this.reportEndDateFilter.getDate(), 23, 59, 59, 999)
      : undefined;
    this.reportService.searchReportSchedules(
      this.selectedFacilityId || this.facilityInputControl.value || undefined,
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
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.selectedFacilityId = null;
    this.reportIdFilter = '';
    this.statusFilters = [];
    this.frequencyFilter = '';
    this.reportStartDateFilter = null;
    this.reportEndDateFilter = null;
    this.createDateFilter = null;
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onFacilitySelected(fac: { facilityId: string; facilityName: string }): void {
    this.selectedFacilityId = fac.facilityId;
    this.facilityInputControl.setValue(fac.facilityName || fac.facilityId, { emitEvent: false });
    this.applyFilters();
  }

  clearFacilityFilter(): void {
    this.selectedFacilityId = null;
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.applyFilters();
  }

  displayFacility(fac: { facilityId: string; facilityName: string } | string | null): string {
    if (!fac) return '';
    if (typeof fac === 'string') return fac;
    return fac.facilityName || fac.facilityId;
  }

  hasActiveFilters(): boolean {
    return !!(this.selectedFacilityId || this.facilityInputControl.value ||
              this.reportIdFilter || this.statusFilters.length > 0 || this.frequencyFilter ||
              this.reportStartDateFilter || this.reportEndDateFilter || this.createDateFilter);
  }

  onShowDeletedChange(): void {
    this.showDeletedSubject.next(this.showDeleted);
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageNumber = event.pageIndex;
    if (event.pageSize > 0) {
      this.paginationMetadata.pageSize = event.pageSize;
      this.defaultPageSize = event.pageSize;
      localStorage.setItem(this.PAGE_SIZE_KEY, event.pageSize.toString());
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
      // Reset to default sort
      this.currentSortBy = 'CreateDate';
      this.currentSortOrder = 1;
    }

    // Reset to first page when sorting changes
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
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

  onSoftDeleteReport(reportScheduleId: string, status: string): void {
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
}
