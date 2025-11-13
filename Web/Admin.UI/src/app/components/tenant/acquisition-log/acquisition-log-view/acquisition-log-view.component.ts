import { animate, style, transition, trigger, keyframes } from '@angular/animations';
import { Location } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { AcquisitionLogSummary } from '../models/acquisition-log-summary';
import { AcquisitionLogService } from '../acquisition-log.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft, faFilter, faPlus, faSort, faSortUp, faSortDown } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { LoadingService } from 'src/app/services/loading.service';
import { finalize, forkJoin } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MatDialogModule } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import { TableCommandComponent } from "./table-command/table-command.component";

@Component({
  selector: 'app-acquisition-log-view',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    FontAwesomeModule,
    MatPaginatorModule,
    MatDialogModule,
    TableCommandComponent
],
  templateUrl: './acquisition-log-view.component.html',
  styleUrl: './acquisition-log-view.component.scss',
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ]),
    ,
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
        animate(
          '100ms cubic-bezier(.4,0,.2,1)',
          style({ opacity: 0, transform: 'scale3d(.9, .9, .9)' })
        )
      ])
    ])
  ]
})
export class AcquisitionLogViewComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFilter = faFilter;
  faPlus = faPlus;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  acquisitionLogs: AcquisitionLogSummary[] = [];
  animatedRows = new Set<string>();
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //filters
  filtersApplied: boolean = false;
  filterPanelOpen = false;
  patientFilter: string = '';
  resourceIdFilter: string = '';
  reportIdFilter: string = '';
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter: string = 'Any';
  resourceTypeFilterOptions: string[] = [];
  selectedResourceTypeFilter: string = 'Any';
  priorityFilterOptions: string[] = [ "Normal", "High", "Critical" ];
  selectedPriorityFilter: string = 'Any';
  queryPhaseFilterOptions: string[] = [ "Initial", "Supplemental", "Referential", "Polling", "Monitoring" ];
  selectedQueryPhaseFilter: string = 'Any';
  queryTypeFilterOptions: string[] = [ "Read", "Search", "BulkDataRequest", "BulkDataPoll" ];
  selectedQueryTypeFilter: string = 'Any';
  statusFilterOptions: string[] = [ "Pending", "Ready", "Processing", "Completed", "Failed", "Cancelled", "MaxRetriesReached"];
  selectedStatusFilter: string = 'Any';

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private loadingService: LoadingService,
    private tenantService: TenantService,
    private acquisitionLogService: AcquisitionLogService) { }

  ngOnInit(): void {

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;

    this.loadingService.show();

    this.route.queryParamMap.subscribe(params => {
      const reportId = params.get('reportId');
      if (reportId) {
        this.reportIdFilter = reportId;
      } else {
        this.reportIdFilter = '';
      }
    });

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.acquisitionLogService.getResourceTypes(),
      this.acquisitionLogService.getAcquisitionLogs(null, null, this.reportIdFilter === '' ? null : this.reportIdFilter, null, null, null, null, null, null, null, null, this.defaultPageNumber, this.defaultPageSize, false)

        ]).subscribe({
          next: (response) => {
            this.facilityFilterOptions = response[0];
            this.resourceTypeFilterOptions = response[1];
            this.acquisitionLogs = response[2].records;
            this.paginationMetadata = response[2].metadata;

            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading audit logs:', error);
            this.loadingService.hide();
          }
        });
  }

  loadLogs(pageNumber: number, pageSize: number, showLoadingIndicator: boolean): void {

    this.acquisitionLogService.getAcquisitionLogs(
      this.patientFilter !== 'Any' ? this.patientFilter : null,
      this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null,
      this.reportIdFilter.length > 0 ? this.reportIdFilter : null,
      null, //this.selectedResourceTypeFilter !== 'Any' ? this.selectedResourceTypeFilter : null,
      this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null,
      this.selectedQueryTypeFilter !== 'Any' ? this.selectedQueryTypeFilter : null,
      this.selectedQueryPhaseFilter !== 'Any' ? this.selectedQueryPhaseFilter : null,
      this.selectedStatusFilter !== 'Any' ? this.selectedStatusFilter : null,
      this.selectedPriorityFilter !== 'Any' ? this.selectedPriorityFilter : null,
      this.sortBy,
      this.sortOrder,
      pageNumber,
      pageSize,
      showLoadingIndicator
    )
    .pipe(
      finalize(() => this.loadingService.hide())
    )
    .subscribe({
      next: (response) => {
        this.acquisitionLogs = response.records;
        this.paginationMetadata = response.metadata;
      },
      error: (error) => {
        console.error('Error loading acquisition logs:', error);
      }
    });
  }

  pagedEvent(event: PageEvent) {
    this.loadLogs(event.pageIndex, event.pageSize, true);
  }

  toggleFilterPanel() {
    this.filterPanelOpen = !this.filterPanelOpen;
  }

  applyFilters(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
    this.filterPanelOpen = false;
    this.onFilterApplication();
  }

  onFilterApplication(): void {
    this.filtersApplied = (this.patientFilter !== '' ||
      this.resourceIdFilter !== '' ||
      this.selectedFacilityFilter !== 'Any' ||
      this.reportIdFilter !== '' ||
      this.selectedResourceTypeFilter !== 'Any' ||
      this.selectedPriorityFilter !== 'Any' ||
      this.selectedQueryPhaseFilter !== 'Any' ||
      this.selectedQueryTypeFilter !== 'Any' ||
      this.selectedStatusFilter !== 'Any');
  }

  refreshLogs(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  clearFilters(): void {
    this.patientFilter = '';
    this.resourceIdFilter = '';
    this.selectedFacilityFilter = 'Any';
    this.reportIdFilter = '';
    this.selectedResourceTypeFilter = 'Any';
    this.selectedPriorityFilter = 'Any';
    this.selectedQueryPhaseFilter = 'Any';
    this.selectedQueryTypeFilter = 'Any';
    this.selectedStatusFilter = 'Any';
    this.filtersApplied = false;
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  onSort(column: string): void {
    if (this.sortBy !== column) {
      this.sortBy = column;
      this.sortOrder = 'ascending';
    } else if (this.sortOrder === 'ascending') {
      this.sortOrder = 'descending';
    } else if (this.sortOrder === 'descending') {
      this.sortBy = null;
      this.sortOrder = null;
    } else {
      this.sortOrder = 'ascending';
    }

    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
  }

  getSortIcon(column: string) {
    if (this.sortBy !== column) return this.faSort;
    if (this.sortOrder === 'ascending') return this.faSortUp;
    if (this.sortOrder === 'descending') return this.faSortDown;

    return this.faSort;
  }

  handleLogScheduled(queryLogId: string) {
    let scheduledLogIndex = this.acquisitionLogs.findIndex((log) => log.id === queryLogId);
    this.acquisitionLogs[scheduledLogIndex].status = 'Ready';
  }

  navBack(): void {
    this.location.back();
  }

}
