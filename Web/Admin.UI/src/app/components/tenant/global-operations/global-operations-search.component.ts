import { animate, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { Location } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import {IOperationModel} from "../../../interfaces/normalization/operation-get-model.interface";
import { ActivatedRoute } from '@angular/router';
import { LoadingService } from 'src/app/services/loading.service';
import { OperationService } from 'src/app/services/gateway/normalization/operation.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotate, faArrowLeft, faFilter, faEye, faEyeSlash, faSort, faSortUp, faSortDown } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { finalize, forkJoin } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { GlobalOperationsTableCommandComponent } from './global-operations-table-command/global-operations-table-command.component';
import { OperationType } from 'src/app/interfaces/normalization/operation-type-enumeration';


@Component({
  selector: 'app-global-operations-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatPaginatorModule,
    FontAwesomeModule,
    GlobalOperationsTableCommandComponent
  ],
  templateUrl: './global-operations-search.component.html',
  styleUrl: './global-operations-search.component.scss',
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ])
  ]
})
export class GlobalOperationsSearchComponent implements OnInit {
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFilter = faFilter;
  faEye = faEye;
  faEyeSlash = faEyeSlash;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  operations: IOperationModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  // Filters
  expandedRow: number | null = null;
  filterPanelOpen = false;
  operationIdFilter: string = '';
  operationTypeFilter: string = 'Any';
  operationTypeOptions: string[] = ['Any', ...OperationService.getOperationTypes()];
  facilityFilter: string = 'Any';
  facilityFilterOptions: Record<string, string> = {};
  resourceFilter: string = 'Any';
  resourceFilterOptions: string[] = [];
  includeDisabledFilter: boolean = false;


  OperationType = OperationType;

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private loadingService: LoadingService,
    private operationsService: OperationService,
    private tenantService: TenantService
  ) {}

  ngOnInit(): void {

    this.loadingService.show();

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.operationsService.getResourceTypes(),
      this.operationsService.searchGlobalOperations(
          null, // facilityId
          this.operationTypeFilter !== 'Any' ? this.operationTypeFilter : null,
          null, // resourceType
          null, // operationId
          this.includeDisabledFilter,
          this.sortBy,
          this.sortOrder,
          this.defaultPageSize,
          this.defaultPageNumber
      )
    ]).pipe(
      finalize(() => this.loadingService.hide())
    ).subscribe({
      next: ([facilities, resourceTypes, operationsSearch]) => {
        this.resourceFilterOptions = ['Any', ...resourceTypes];
        this.facilityFilterOptions = facilities;
        this.operations = operationsSearch.records;
        this.paginationMetadata = operationsSearch.metadata;
        console.info('Loaded operations:', this.operations);
        this.loadingService.hide();
      }
      ,
      error: (error) => {
        console.error('Error loading operations:', error);
        this.loadingService.hide();
      }
    });

  }

  loadOperations(pageNumber: number, pageSize: number): void {
    this.loadingService.show();

    this.expandedRow = null; // Reset expanded row on new search

    this.operationsService.searchGlobalOperations(
      this.facilityFilter !== 'Any' ? this.facilityFilter : null,
      this.operationTypeFilter !== 'Any' ? this.operationTypeFilter : null,
      this.resourceFilter !== 'Any' ? this.resourceFilter : null,
      this.operationIdFilter.length > 0 ? this.operationIdFilter : null,
      this.includeDisabledFilter,
      this.sortBy,
      this.sortOrder,
      pageSize,
      pageNumber
    ).pipe(
      finalize(() => this.loadingService.hide())
    ).subscribe({
      next: (response) => {
        this.operations = response.records;
        console.info('Loaded operations:', this.operations);
        this.paginationMetadata = response.metadata;
      },
      error: (error) => {
        console.error('Error loading operations:', error);
      }
    });
  }

  getResourceNames(resources: any[]): string {
    return resources && resources.length ? resources.map(r => r.resourceName).join(', ') : '';
  }

  toggleOperationDetails(index: number): void {
    this.expandedRow = this.expandedRow === index ? null : index;
  }

  pagedEvent(event: PageEvent) {
       this.loadOperations(event.pageIndex, event.pageSize);
  }

  toggleFilterPanel() {
    this.filterPanelOpen = !this.filterPanelOpen;
  }

  applyFilters(): void {
    this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
    this.filterPanelOpen = false;
  }

  clearFilters(): void {
    this.operationIdFilter = '';
    this.facilityFilter = 'Any';
    this.resourceFilter = 'Any';
    this.operationTypeFilter = 'Any';
    this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
  }

  toggleDisabledInclusion(): void {
    this.includeDisabledFilter = !this.includeDisabledFilter;
    this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
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
    this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
  }

  getSortIcon(column: string) {
    if (this.sortBy !== column) return this.faSort;
    if (this.sortOrder === 'ascending') return this.faSortUp;
    if (this.sortOrder === 'descending') return this.faSortDown;
    return this.faSort;
  }

  onRefresh(): void {
    this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
  }

  navBack(): void {
    this.location.back();
  }
}
