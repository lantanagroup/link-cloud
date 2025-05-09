import { animate, style, transition, trigger } from '@angular/animations';
import { Location } from '@angular/common';
import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { AcquisitionLogSummary } from '../models/acquisition-log-summary';
import { AcquisitionLogService } from '../acquisition-log.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';

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
export class AcquisitionLogViewComponent {
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
  facilityFilterOptions: string[] = [];
  selectedFacilityFilter: string = 'any';
  resourceTypeFilterOptions: string[] = [];
  selectedResourceTypeFilter: string = 'any';
  priorityFilterOptions: string[] = [];
  selectedPriorityFilter: string = 'any';
  queryPhaseFilterOptions: string[] = [];
  selectedQueryPhaseFilter: string = 'any';
  queryTypeFilterOptions: string[] = [];
  selectedQueryTypeFilter: string = 'any';
  statusFilterOptions: string[] = [];
  selectedStatusFilter: string = 'any';

  constructor(
    private location: Location,
    private acquisitionLogService: AcquisitionLogService) {   
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize);    
  }

  loadLogs(pageNumber: number, pageSize: number): void {

    let patientId: string | null = this.patientFilter.length > 0 ? this.patientFilter : null;
    let resourceId: string | null = this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null;
    let facility: string | null = this.selectedFacilityFilter === 'any' ? null : this.selectedFacilityFilter;
    let resourceType: string | null = this.selectedResourceTypeFilter === 'any' ? null : this.selectedResourceTypeFilter;
    let priority: string | null = this.selectedPriorityFilter === 'any' ? null : this.selectedPriorityFilter;
    let queryPhase: string | null = this.selectedQueryPhaseFilter === 'any' ? null : this.selectedQueryPhaseFilter;
    let queryType: string | null = this.selectedQueryTypeFilter === 'any' ? null : this.selectedQueryTypeFilter;
    let status: string | null = this.selectedStatusFilter === 'any' ? null : this.selectedStatusFilter;

    // this.acquisitionLogService.getAcquisitionLogs().subscribe((logs: AcquisitionLogSummary[]) => {
    //   this.acquisitionLogs = logs
    // });

    //create test data for 3 acquisition logs
    this.acquisitionLogs = [
      {
        id: '1',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Patient'],
        resourceId: '12345',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Read',
        scheduledDate: new Date(),
        status: 'Completed'
      },
      {
        id: '2',
        priority: 'Normal',
        patientId: '12345',
        facilityId: 'TestFacility',
        resourceTypes: ['Encounter'],
        resourceId: '',
        fhirVersion: 'R4',
        queryPhase: 'Initial',
        queryType: 'Search',
        scheduledDate: new Date(),
        status: 'Pending'
      }
    ];
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
