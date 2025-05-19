import { animate, style, transition, trigger } from '@angular/animations';
import { Location } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { AcquisitionLogSummary } from '../models/acquisition-log-summary';
import { AcquisitionLogService } from '../acquisition-log.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft, faFilter, faPlus } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { LoadingService } from 'src/app/services/loading.service';
import { forkJoin } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { AcquisitionLog } from '../models/acquisition-log';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { AcquisitionLogDetailsComponent } from '../acquisition-log-details/acquisition-log-details.component';

@Component({
  selector: 'app-acquisition-log-view',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    FontAwesomeModule,
    MatPaginatorModule,
    MatDialogModule
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
  faFilter = faFilter;
  faPlus = faPlus; 

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  acquisitionLogs: AcquisitionLogSummary[] = [];
  animatedRows = new Set<string>();
  paginationMetadata: PaginationMetadata = new PaginationMetadata;  
  
  //filters
  filterPanelOpen = false;
  patientFilter: string = '';
  resourceIdFilter: string = '';
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter: string = 'Any';
  resourceTypeFilterOptions: string[] = [];
  selectedResourceTypeFilter: string = 'Any';
  priorityFilterOptions: string[] = [ "Nomral", "High", "Critical" ];
  selectedPriorityFilter: string = 'Any';
  queryPhaseFilterOptions: string[] = [ "Initial", "Supplemental", "Referential", "Polling", "Monitoring" ];
  selectedQueryPhaseFilter: string = 'Any';
  queryTypeFilterOptions: string[] = [ "Read", "Search", "BulkDataReqeust", "BulkDataPoll" ];
  selectedQueryTypeFilter: string = 'Any';
  statusFilterOptions: string[] = [];
  selectedStatusFilter: string = 'Any';

  constructor(
    private location: Location,
    private loadingService: LoadingService,
    private dialog: MatDialog,
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
    let facility: string | null = this.selectedFacilityFilter === 'Any' ? null : this.selectedFacilityFilter;
    let resourceType: string | null = this.selectedResourceTypeFilter === 'Any' ? null : this.selectedResourceTypeFilter;
    let resourceId: string | null = this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null;   
    let queryType: string | null = this.selectedQueryTypeFilter === 'Any' ? null : this.selectedQueryTypeFilter;    
    let queryPhase: string | null = this.selectedQueryPhaseFilter === 'Any' ? null : this.selectedQueryPhaseFilter;    
    let status: string | null = this.selectedStatusFilter === 'Any' ? null : this.selectedStatusFilter;
    let priority: string | null = this.selectedPriorityFilter === 'Any' ? null : this.selectedPriorityFilter;

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

  toggleFilterPanel() {
    this.filterPanelOpen = !this.filterPanelOpen;
  }

  applyFilters(): void {
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
    this.filterPanelOpen = false;
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
    this.selectedFacilityFilter = 'Any';
    this.selectedResourceTypeFilter = 'Any';
    this.selectedPriorityFilter = 'Any';
    this.selectedQueryPhaseFilter = 'Any';
    this.selectedQueryTypeFilter = 'Any';
    this.selectedStatusFilter = 'Any';
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);
  }

  onAcquisitionLogSelected(measureReport: AcquisitionLogSummary): void {

    //get acquisitin log details
    this.acquisitionLogService.getAcquisitionLog(measureReport.id).subscribe({
      next: (response) => {
        this.openLogDetails(response);
      },
      error: (error) => {
        console.error('Error loading acquisition log details:', error);
      }
    });    
  }

  openLogDetails(log: AcquisitionLog): void {
    
    const dialogConfig = new MatDialogConfig();
    dialogConfig.minWidth = '90vw';
    dialogConfig.maxHeight = '85vh';
    dialogConfig.panelClass = 'link-dialog-container';
    dialogConfig.data = {
      dialogTitle: 'Acquisition Log Details',
      acquisitionLog: log,      
    };

      this.dialog.open(AcquisitionLogDetailsComponent, dialogConfig);

  }

  navBack(): void {
    this.location.back();
  }

}
