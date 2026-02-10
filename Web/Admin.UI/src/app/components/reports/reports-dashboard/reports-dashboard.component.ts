import { Component, OnInit, ViewChild, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatSortModule, MatSort, Sort } from '@angular/material/sort';
import { MatTooltipModule } from '@angular/material/tooltip';
import { forkJoin, Subscription } from 'rxjs';
import { TenantService } from '../../../services/gateway/tenant/tenant.service';
import { LoadingService } from '../../../services/loading.service';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { IReportListSummary, IPagedReportListSummary } from '../../tenant/facility-view/report-view.interface';
import { CommonModule } from '@angular/common';
import { ResubmitDialogComponent } from "../../tenant/facility-view/resubmit-dialog.component";
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotate } from '@fortawesome/free-solid-svg-icons';
import { IReportSchedule } from '../../../interfaces/report/report-schedule.interface';
import { ReportService } from '../../../services/gateway/report/report.service';
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
    MatCheckbox
  ],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent implements OnInit, OnDestroy {
  @ViewChild(MatPaginator, { static: false }) paginator!: MatPaginator;
  @ViewChild(MatSort, { static: false }) sort!: MatSort;

  private subscription: Subscription | undefined;
  defaultPageNumber: number = 0;
  defaultPageSize: number = 10;
  paginationMetadata: PaginationMetadata = new PaginationMetadata();
  faRotate = faRotate;
  showDeleted = false;

  dataSource = new MatTableDataSource<IReportSchedule>([]);
  reportSchedules: IReportSchedule[] = [];

  currentSortBy: string = 'CreateDate';
  currentSortOrder: number = 1; // 1 = Descending, 0 = Ascending

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private reportService: ReportService,
    private loadingService: LoadingService,
    private dialog: MatDialog,
    private tenantService: TenantService,) {
  }

  ngOnInit(): void {
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
          next: response => {
            // refresh list or show toast
            this.onRefresh();
          },
          error: err => {
            console.error('Resubmit failed', err);
          }
        });
    });
  }
  onRefresh() {
    this.loadReportSchedules();
  }
}
