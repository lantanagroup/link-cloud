import { animate, style, transition, trigger } from '@angular/animations';
import { Location } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { AcquisitionLogSummary } from '../models/acquisition-log-summary';
import { AcquisitionLogService } from '../acquisition-log.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { LoadingService } from 'src/app/services/loading.service';
import { forkJoin } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';

@Component({
  selector: 'app-acquisition-log-view',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    FontAwesomeModule,
    MatPaginatorModule
  ],
  templateUrl: './acquisition-log-view.component.html',
  styleUrl: './acquisition-log-view.component.scss',
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ]
})
export class AcquisitionLogViewComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;  

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  acquisitionLogs: AcquisitionLogSummary[] = [];
  animatedRows = new Set<string>();
  paginationMetadata: PaginationMetadata = new PaginationMetadata;  
  
  //filters
  patientFilter: string = '';
  resourceIdFilter: string = '';
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter: string = 'any';
  resourceTypeFilterOptions: string[] = [];
  selectedResourceTypeFilter: string = 'any';
  priorityFilterOptions: string[] = [ "Nomral", "High", "Critical" ];
  selectedPriorityFilter: string = 'any';
  queryPhaseFilterOptions: string[] = [ "Initial", "Supplemental", "Referential", "Polling", "Monitoring" ];
  selectedQueryPhaseFilter: string = 'any';
  queryTypeFilterOptions: string[] = [ "Read", "Search", "BulkDataReqeust", "BulkDataPoll" ];
  selectedQueryTypeFilter: string = 'any';
  statusFilterOptions: string[] = [];
  selectedStatusFilter: string = 'any';

  constructor(
    private location: Location,
    private loadingService: LoadingService,
    private tenantService: TenantService,
    private acquisitionLogService: AcquisitionLogService) { }

  ngOnInit(): void {

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;

    this.loadingService.show();

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.acquisitionLogService.getResourceTypes(),
      this.acquisitionLogService.getAcquisitionLogs(null, null, null, null, null, null, null, null, this.defaultPageNumber, this.defaultPageSize, false)
      
        ]).subscribe({
          next: (response) => {
            this.facilityFilterOptions = response[0];
            this.resourceTypeFilterOptions = response[1];
            this.acquisitionLogs = response[2];
            //this.acquisitionLogs = response[2].records;                
            //this.paginationMetadata = response[2].metadata;
            
            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading audit logs:', error);
            this.loadingService.hide();
          }
        });       
  }

  loadLogs(pageNumber: number, pageSize: number): void {

    let patientId: string | null = this.patientFilter.length > 0 ? this.patientFilter : null;
    let facility: string | null = this.selectedFacilityFilter === 'any' ? null : this.selectedFacilityFilter;
    let resourceType: string | null = this.selectedResourceTypeFilter === 'any' ? null : this.selectedResourceTypeFilter;
    let resourceId: string | null = this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null;   
    let queryType: string | null = this.selectedQueryTypeFilter === 'any' ? null : this.selectedQueryTypeFilter;    
    let queryPhase: string | null = this.selectedQueryPhaseFilter === 'any' ? null : this.selectedQueryPhaseFilter;    
    let status: string | null = this.selectedStatusFilter === 'any' ? null : this.selectedStatusFilter;
    let priority: string | null = this.selectedPriorityFilter === 'any' ? null : this.selectedPriorityFilter;

    this.acquisitionLogService.getAcquisitionLogs(patientId, facility, resourceType, resourceId, queryType, queryPhase, status, priority, pageNumber, pageSize, true)
    .subscribe({
      next: (response) => {
         this.acquisitionLogs = response;
        // this.acquisitionLogs = response.records;
        // this.paginationMetadata = response.metadata;      
      },
      error: (error) => {
        console.error('Error loading acquisition logs:', error);
      }
    });    
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadLogs(event.pageIndex, event.pageSize);
  }

  onPatientIdChange(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
  }

  onResourceIdChange(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
  }

  onFacilityFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  onResourceTypeFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  onPriorityFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  onQueryPhaseFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  onQueryTypeFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  onStatusFilterChange(event: Event): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);  
  }

  refreshLogs(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
  }

  clearFilters(): void {
    this.patientFilter = '';
    this.resourceIdFilter = '';
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
  }

  navBack(): void {
    this.location.back();
  }

}
