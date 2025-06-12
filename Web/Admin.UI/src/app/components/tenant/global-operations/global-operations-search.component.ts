import { animate, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { Location } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { OperationModel } from './models/opeation-model';
import { ActivatedRoute } from '@angular/router';
import { LoadingService } from 'src/app/services/loading.service';
import { OperationService } from 'src/app/services/gateway/normalization/operation.service';


@Component({
  selector: 'app-global-operations-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatPaginatorModule   
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
  operations: OperationModel[] = [];
  filterPanelOpen = false;
  descriptionFilter: string = '';
  operationTypeFilter: string = 'Any';
  operationTypeOptions: string[] = ['Any', 'TypeA', 'TypeB', 'TypeC']; // Replace with real types
  isDisabledFilter: boolean = false;
  pageNumber: number = 0;
  pageSize: number = 10;
  totalCount: number = 0;

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private loadingService: LoadingService,
    private operationsService: OperationService
  ) {}

  ngOnInit(): void {
    this.loadOperations(this.pageNumber, this.pageSize);
  }

  loadOperations(pageNumber: number, pageSize: number): void {
    this.loadingService.show();
    this.operationsService.searchGlobalOperations(
      "TestFacilityOne", // facilityId
      this.operationTypeFilter !== 'Any' ? this.operationTypeFilter : null,
      null, // resourceType
      null, // operationId
      this.isDisabledFilter,
      null, // sortBy
      null, // sortOrder
      pageSize,
      pageNumber
    ).subscribe({
      next: (response) => {
        this.operations = response.records;
        this.totalCount = response.metadata.totalCount;
        this.loadingService.hide();
      },
      error: (error) => {
        console.error('Error loading operations:', error);
        this.loadingService.hide();
      }
    });
  }

  pagedEvent(event: PageEvent) {
    this.pageSize = event.pageSize;
    this.pageNumber = event.pageIndex;
    this.loadOperations(this.pageNumber, this.pageSize);
  }

  toggleFilterPanel() {
    this.filterPanelOpen = !this.filterPanelOpen;
  }

  applyFilters(): void {
    this.loadOperations(0, this.pageSize);
    this.filterPanelOpen = false;
  }

  clearFilters(): void {
    this.descriptionFilter = '';
    this.operationTypeFilter = 'Any';
    this.isDisabledFilter = false;
    this.loadOperations(0, this.pageSize);
  }
}
