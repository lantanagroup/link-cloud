import { animate, style, transition, trigger, keyframes } from '@angular/animations';
import { Location, CommonModule, KeyValuePipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft, faFilter, faSort, faSortUp, faSortDown, faEye, faRefresh } from '@fortawesome/free-solid-svg-icons';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { finalize, forkJoin } from 'rxjs';
import { LoadingService } from 'src/app/services/loading.service';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { SftpAcquisitionLogSummary } from '../models/sftp-acquisition-log-summary';
import { SftpAcquisitionLogService } from '../sftp-acquisition-log.service';
import { SftpAcquisitionLogDetailsComponent } from '../sftp-acquisition-log-details/sftp-acquisition-log-details.component';
import { SftpAcquisitionType } from '../../../../interfaces/data-acquisition/sftp-config-model.interface';

@Component({
  selector: 'app-sftp-acquisition-log-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    FontAwesomeModule,
    MatPaginatorModule,
    MatDialogModule,
    KeyValuePipe
  ],
  templateUrl: './sftp-acquisition-log-view.component.html',
  styleUrls: ['./sftp-acquisition-log-view.component.scss'],
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ]),
    trigger('fadeGrowRightOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scaleX(0.5) scaleY(0.8) translateX(40px) translateY(10px)' }),
        animate('250ms cubic-bezier(.4,0,.2,1)', style({ opacity: 1, transform: 'scaleX(1) scaleY(1) translateX(0) translateY(0)' }))
      ])
    ]),
    trigger('fadeInOutScale', [
      transition(':enter', [
        animate(
          '600ms cubic-bezier(.23,1.02,.57,1.01)',
          keyframes([
            style({ opacity: 0, transform: 'scale3d(.9, .9, .9)', offset: 0 }),
            style({ opacity: 1, transform: 'scale3d(1.1, 1.1, 1.1)', offset: 0.4 }),
            style({ transform: 'scale3d(0.95, 0.95, 0.95)', offset: 0.6 }),
            style({ transform: 'scale3d(1.02, 1.02, 1.02)', offset: 0.8 }),
            style({ opacity: 1, transform: 'scale3d(1, 1, 1)', offset: 1 })
          ])
        )
      ]),
      transition(':leave', [
        animate('100ms cubic-bezier(.4,0,.2,1)', style({ opacity: 0, transform: 'scale3d(.9, .9, .9)' }))
      ])
    ])
  ]
})
export class SftpAcquisitionLogViewComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFilter = faFilter;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;
  faEye = faEye;
  faRefresh = faRefresh;

  defaultPageNumber = 0;
  defaultPageSize = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  sftpLogs: SftpAcquisitionLogSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata();

  // Filters
  filtersApplied = false;
  filterPanelOpen = false;
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter = 'Any';
  acquisitionTypeFilterOptions = Object.values(SftpAcquisitionType);
  selectedAcquisitionTypeFilter = 'Any';
  statusFilterOptions = ['Pending', 'Processing', 'Completed', 'Failed', 'MaxRetriesReached', 'ConfigurationRequired'];
  selectedStatusFilter = 'Any';

  constructor(
    private location: Location,
    private dialog: MatDialog,
    private loadingService: LoadingService,
    private tenantService: TenantService,
    private sftpLogService: SftpAcquisitionLogService
  ) { }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;

    this.loadingService.show();

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.sftpLogService.getSftpAcquisitionLogs(
        null, null, null, null, null,
        this.defaultPageNumber, this.defaultPageSize, false
      )
    ]).subscribe({
      next: ([facilities, logsResponse]) => {
        this.facilityFilterOptions = facilities;
        this.sftpLogs = logsResponse.records;
        this.paginationMetadata = logsResponse.metadata as PaginationMetadata;
        this.loadingService.hide();
      },
      error: (error) => {
        console.error('Error loading SFTP acquisition logs:', error);
        this.loadingService.hide();
      }
    });
  }

  loadLogs(pageNumber: number, pageSize: number, showLoadingIndicator: boolean): void {
    this.sftpLogService.getSftpAcquisitionLogs(
      this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null,
      this.selectedAcquisitionTypeFilter !== 'Any' ? this.selectedAcquisitionTypeFilter : null,
      this.selectedStatusFilter !== 'Any' ? this.selectedStatusFilter : null,
      this.sortBy,
      this.sortOrder,
      pageNumber,
      pageSize,
      showLoadingIndicator
    )
    .pipe(finalize(() => this.loadingService.hide()))
    .subscribe({
      next: (response) => {
        this.sftpLogs = response.records;
        this.paginationMetadata = response.metadata as PaginationMetadata;
      },
      error: (error) => {
        console.error('Error loading SFTP acquisition logs:', error);
      }
    });
  }

  pagedEvent(event: PageEvent): void {
    this.loadLogs(event.pageIndex, event.pageSize, true);
  }

  toggleFilterPanel(): void {
    this.filterPanelOpen = !this.filterPanelOpen;
  }

  applyFilters(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
    this.filterPanelOpen = false;
    this.filtersApplied = (
      this.selectedFacilityFilter !== 'Any' ||
      this.selectedAcquisitionTypeFilter !== 'Any' ||
      this.selectedStatusFilter !== 'Any'
    );
  }

  clearFilters(): void {
    this.selectedFacilityFilter = 'Any';
    this.selectedAcquisitionTypeFilter = 'Any';
    this.selectedStatusFilter = 'Any';
    this.filtersApplied = false;
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  refreshLogs(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  onSort(column: string): void {
    if (this.sortBy !== column) {
      this.sortBy = column;
      this.sortOrder = 'ascending';
    } else if (this.sortOrder === 'ascending') {
      this.sortOrder = 'descending';
    } else {
      this.sortBy = null;
      this.sortOrder = null;
    }
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  getSortIcon(column: string) {
    if (this.sortBy !== column) return this.faSort;
    if (this.sortOrder === 'ascending') return this.faSortUp;
    if (this.sortOrder === 'descending') return this.faSortDown;
    return this.faSort;
  }

  viewDetails(log: SftpAcquisitionLogSummary): void {
    this.sftpLogService.getSftpAcquisitionLog(log.id).subscribe({
      next: (fullLog) => {
        const dialogRef = this.dialog.open(SftpAcquisitionLogDetailsComponent, {
          width: '800px',
          maxHeight: '90vh',
          data: {
            dialogTitle: `SFTP Acquisition Log - ${fullLog.externalId}`,
            sftpAcquisitionLog: fullLog
          }
        });

        dialogRef.afterClosed().subscribe(result => {
          if (result?.reset) {
            this.loadLogs(this.paginationMetadata.pageNumber, this.paginationMetadata.pageSize, false);
          }
        });
      },
      error: (error) => {
        console.error('Error loading SFTP acquisition log details:', error);
      }
    });
  }

  navBack(): void {
    this.location.back();
  }

  isResettable(log: SftpAcquisitionLogSummary): boolean {
    return log.status === 'ConfigurationRequired' || log.status === 'MaxRetriesReached';
  }

  resetLog(log: SftpAcquisitionLogSummary): void {
    this.loadingService.show();
    this.sftpLogService.resetSftpAcquisitionLog(log.externalId)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: () => {
          this.loadLogs(this.paginationMetadata.pageNumber, this.paginationMetadata.pageSize, false);
        },
        error: (error) => {
          console.error('Error resetting SFTP acquisition log:', error);
        }
      });
  }
}
