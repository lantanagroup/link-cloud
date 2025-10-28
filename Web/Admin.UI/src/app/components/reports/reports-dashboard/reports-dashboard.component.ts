import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { forkJoin, Subscription } from 'rxjs';
import { TenantService } from '../../../services/gateway/tenant/tenant.service';
import { LoadingService } from '../../../services/loading.service';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { IReportListSummary, IPagedReportListSummary } from '../../tenant/facility-view/report-view.interface';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { CommonModule } from '@angular/common';
import { ResubmitDialogComponent } from "../../tenant/facility-view/resubmit-dialog.component";
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotate } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-reports-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatPaginatorModule,
    RouterLink,
    FontAwesomeModule
  ],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent implements OnInit {
  private subscription: Subscription | undefined;
  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  reportListSummary: IReportListSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  faRotate = faRotate;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private facilityViewService: FacilityViewService,
    private loadingService: LoadingService,
    private dialog: MatDialog,
    private tenantService: TenantService,) {
  }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {

      this.loadingService.show();

      forkJoin([
        this.facilityViewService.getReportSummaryList('', this.defaultPageNumber, this.defaultPageSize)
      ]).subscribe({
        next: (response) => {

          this.reportListSummary = response[0].records;
          this.paginationMetadata = response[0].metadata;

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
  }

  navigateTo(path: string) {
    this.router.navigateByUrl(path);
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadReportSummaryList(event.pageIndex, event.pageSize);
  }

  loadReportSummaryList(pageNumber: number, pageSize: number): void {
    this.facilityViewService.getReportSummaryList('', pageNumber, pageSize).subscribe({
      next: (response: IPagedReportListSummary) => {
        this.reportListSummary = response.records;
        this.paginationMetadata = response.metadata;
      },
      error: (error) => {
        console.error('Error fetching facility report summaries:', error);
      }
    });
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
      this.loadReportSummaryList(this.defaultPageNumber, this.defaultPageSize);
    }
}
