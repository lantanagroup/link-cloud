import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {MatDialog} from '@angular/material/dialog';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatPaginator, MatPaginatorModule, PageEvent} from '@angular/material/paginator';
import {MatSort, MatSortModule, Sort} from '@angular/material/sort';
import {MatTooltipModule} from '@angular/material/tooltip';
import {Subscription} from 'rxjs';
import {TenantService} from '../../../services/gateway/tenant/tenant.service';
import {LoadingService} from '../../../services/loading.service';
import {PaginationMetadata} from '../../../models/pagination-metadata.model';
import {CommonModule} from '@angular/common';
import {ResubmitDialogComponent} from "../../tenant/facility-view/resubmit-dialog.component";
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {faRotate} from '@fortawesome/free-solid-svg-icons';
import {IReportSchedule} from '../../../interfaces/report/report-schedule.interface';
import {ReportService} from '../../../services/gateway/report/report.service';
import {FormsModule} from "@angular/forms";
import {MatCheckbox} from "@angular/material/checkbox";

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
    MatCheckbox,
    MatSnackBarModule
  ],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent implements OnInit, OnDestroy {
  @ViewChild(MatPaginator, { static: false }) paginator!: MatPaginator;
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  private subscription: Subscription | undefined;
  private readonly PAGE_SIZE_KEY = 'reportsDashboardPageSize';
  defaultPageNumber: number = 0;
  defaultPageSize: number = 10;
  paginationMetadata: PaginationMetadata = new PaginationMetadata();
  faRotate = faRotate;
  showDeleted = false;

  dataSource = new MatTableDataSource<IReportSchedule>([]);
  reportSchedules: IReportSchedule[] = [];
  highlightedRowIds = new Set<string>();
  private beforeResubmitIds: Set<string> | null = null;

  currentSortBy: string = 'CreateDate';
  currentSortOrder: number = 1; // 1 = Descending, 0 = Ascending

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private reportService: ReportService,
    private loadingService: LoadingService,
    private dialog: MatDialog,
    private tenantService: TenantService,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    const savedPageSize = localStorage.getItem(this.PAGE_SIZE_KEY);
    if (savedPageSize) {
      this.defaultPageSize = +savedPageSize;
    }

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.loadReportSchedules();
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  getColumns(): string[] {
    const cols = ['id', 'facilityId', 'reportStartDate', 'frequency', 'reportTypes', 'patientsInCensus', 'patientsInIP', 'status', 'action'];
    if (this.showDeleted) cols.push('isDeleted');
    return cols;
  }

  loadReportSchedules(): void {
    this.loadingService.isLoading.next(true);
    this.reportService.searchReportSchedules(
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      this.showDeleted,
      this.currentSortBy,
      this.currentSortOrder,
      this.paginationMetadata.pageSize,
      this.paginationMetadata.pageNumber + 1 // API expects 1-based indexing
    ).subscribe({
      next: (data) => {
        this.reportSchedules = data.records;
        this.dataSource.data = this.reportSchedules;
        this.paginationMetadata = data.metadata;
        this.paginationMetadata.pageNumber = data.metadata.pageNumber - 1; // Convert back to 0-based
        this.loadingService.isLoading.next(false);
        if (this.beforeResubmitIds) {
          const newIds = data.records.filter(r => !this.beforeResubmitIds!.has(r.id)).map(r => r.id);
          this.highlightedRowIds = new Set(newIds);
          this.beforeResubmitIds = null;
          if (newIds.length > 0) {
            setTimeout(() => this.highlightedRowIds = new Set(), 4000);
          }
        }
      },
      error: (error) => {
        console.error('Error loading report schedules:', error);
        this.loadingService.isLoading.next(false);
      }
    });
  }

  onShowDeletedChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.loadReportSchedules();
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    localStorage.setItem(this.PAGE_SIZE_KEY, event.pageSize.toString());
    this.loadReportSchedules();
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction) {
      // Map UI column names to API field names
      const sortFieldMap: { [key: string]: string } = {
        'id': 'Id',
        'facilityId': 'FacilityId',
        'reportStartDate': 'ReportStartDate',
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
            setTimeout(() => {
              this.beforeResubmitIds = new Set(this.reportSchedules.map(r => r.id));
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
