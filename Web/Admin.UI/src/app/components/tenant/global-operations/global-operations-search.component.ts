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
import { faRotate, faArrowLeft, faFilter, faEye, faEyeSlash, faSort, faSortUp, faSortDown, faAdd } from '@fortawesome/free-solid-svg-icons';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { finalize, forkJoin } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { GlobalOperationsTableCommandComponent } from './global-operations-table-command/global-operations-table-command.component';
import { OperationType } from 'src/app/interfaces/normalization/operation-type-enumeration';
import {MatExpansionPanelActionRow} from "@angular/material/expansion";
import {MatMenu, MatMenuItem, MatMenuTrigger} from "@angular/material/menu";
import {OperationDialogComponent} from "../../normalization/operations/operation-dialog/operation-dialog.component";
import {FormMode} from "../../../models/FormMode.enum";
import {SnackbarHelper} from "../../../services/snackbar-helper";
import {MatDialog} from "@angular/material/dialog";
import {MatIcon} from "@angular/material/icon";
import {MatTooltip} from "@angular/material/tooltip";
import {MatSnackBar} from "@angular/material/snack-bar";
import {MatCell, MatCellDef, MatColumnDef, MatHeaderCell} from "@angular/material/table";
import {CodeSystemMap} from "../../../interfaces/normalization/code-map-operation-interface";
import {IVendor} from "../../../interfaces/normalization/vendor-interface";


@Component({
  selector: 'app-global-operations-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatPaginatorModule,
    FontAwesomeModule,
    MatExpansionPanelActionRow,
    MatIcon,
    MatMenu,
    MatMenuItem,
    MatTooltip,
    MatMenuTrigger,
    MatCell,
    MatCellDef,
    MatColumnDef,
    MatHeaderCell
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
  vendorFilterOptions: Record<string, string> = {};
  resourceFilter: string = 'Any';
  resourceFilterOptions: string[] = [];
  vendorFilter: string = 'Any';
  includeDisabledFilter: boolean = false;


  OperationType = OperationType;

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private loadingService: LoadingService,
    private operationsService: OperationService,
    private tenantService: TenantService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {

    this.loadingService.show();

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.operationsService.getResourceTypes(),
      this.operationsService.getVendors(),
      this.operationsService.searchGlobalOperations(
          null, // facilityId
          this.operationTypeFilter !== 'Any' ? this.operationTypeFilter : null,
          null, // resourceType
          null, // operationId
          this.includeDisabledFilter,
          null, //vendorId
          this.sortBy,
          this.sortOrder,
          this.defaultPageSize,
          this.defaultPageNumber
      )
    ]).pipe(
      finalize(() => this.loadingService.hide())
    ).subscribe({
      next: ([facilities, resourceTypes, vendors, operationsSearch]) => {
        this.resourceFilterOptions = ['Any', ...resourceTypes];
        this.facilityFilterOptions = facilities;
        this.vendorFilterOptions = vendors.reduce((acc, vendor) => {acc[vendor.id] = vendor.name; return acc;}, {} as Record<string, string>);
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
      this.vendorFilter !== 'Any' ? this.vendorFilter : null,
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

  getResourceNames(operationResourceTypes: any[]): string {
    return operationResourceTypes && operationResourceTypes.length
      ? operationResourceTypes
        .map(r => r.resource?.resourceName)
        .filter((name): name is string => !!name) // remove null/undefined
        .join(', ')
      : '';
  }

  getVendorNames(op: IOperationModel){
    return op.vendorPresets
        .map(p => p.vendorVersion?.vendor?.name)
        .filter(name => !!name) // remove undefined/null
        .join(', ');
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
    this.vendorFilter = 'Any';
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

  showOperationDialog(operationType: OperationType) {
    this.dialog.open(OperationDialogComponent,
      {
        width: '50vw',
        maxWidth: '50vw',
        data: {
          dialogTitle: 'Add ' + this.toDescription(operationType.toString()),
          formMode: FormMode.Create,
          operationType: operationType,
          operation: {} as IOperationModel,
          viewOnly: false
        }
      }).afterClosed().subscribe(res => {
      if(res) {
        SnackbarHelper.showSuccessMessage(this.snackBar, res);
        this.loadOperations(this.defaultPageNumber, this.defaultPageSize)
      }
    });
  }

  showOperationEditDialog(operation: IOperationModel) {
    this.dialog.open(OperationDialogComponent,
      {
        width: '50vw',
        maxWidth: '50vw',
        data: {
          dialogTitle: 'Edit ' + this.toDescription(operation.operationType),
          formMode: FormMode.Edit,
          operationType: operation.operationType,
          operation: operation,
          viewOnly: false
        }
      }).afterClosed().subscribe(res => {
      if(res) {
        SnackbarHelper.showSuccessMessage(this.snackBar, res);
        this.loadOperations(this.defaultPageNumber, this.defaultPageSize)
      }
    });
  }


  deleteOperation(operation: IOperationModel){
    const resourceName = operation.operationResourceTypes?.[0]?.resource?.resourceName??"";

    if (operation.facilityId !== null) {
      this.operationsService
          .deleteOperationByFacility(operation.facilityId, operation.id, resourceName)
          .subscribe(res => {
            this.loadOperations(this.defaultPageNumber, this.defaultPageSize);
          });
    }
    else{ // delete vendor operation
     // this.operationsService.deleteOperationByVendor("Epic").subscribe(res => { });
    }
  }

  toDescription(enumValue: string): string {
    // Insert a space before each uppercase letter that is preceded by a lowercase letter or number
    return enumValue.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  }

  protected readonly faAdd = faAdd;
}
