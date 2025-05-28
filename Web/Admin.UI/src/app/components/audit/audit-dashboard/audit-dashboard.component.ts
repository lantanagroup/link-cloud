import { Component, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { AuditModel } from '../../../models/audit/audit-model.model';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ITableHeaderModel } from '../../../interfaces/table-header-model.interface';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { forkJoin } from 'rxjs';
import { LoadingService } from 'src/app/services/loading.service';
import { AuditService } from '../audit.service';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-audit-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatTableModule,
    MatPaginatorModule,
    MatExpansionModule,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatToolbarModule
    ],
  templateUrl: './audit-dashboard.component.html',
  styleUrls: ['./audit-dashboard.component.scss'],
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: '*' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
})
export class AuditDashboardComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  defaultSortBy: string = 'EventDate';

  auditLogs: AuditModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  displayedColumns: ITableHeaderModel[] = [{ key: 'id', display: 'Id' }, { key: 'facilityId', display: 'Facility' }, { key: 'correlationId', display: 'Correlation Id' }, { key: 'serviceName', display: 'Service Name' }, { key: 'action', display: 'Action' }, { key: 'eventDate', display: 'Date' }];
  columnsToDisplayWithExpand = [...this.displayedColumns.map(x => x.key), 'expand'];
  expandedRecord: AuditModel | null | undefined;
  dataSource = new MatTableDataSource<AuditModel>(this.auditLogs);

  //filters
  searchText: string = '';
  correlationId: string = '';
  user: string = '';
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter: string = 'Any';
  selectedServiceFilter: string = 'Any';
  actionFilterOptions = ["Create", "Update", "Delete", "Query", "Submission"];
  selectedActionFilter: string = 'Any';

  serviceFilterOptions : Record<string, string> = {
      "Account Service": "Account",
      "Census Service": "Census",
      "Data Acquisition": "DataAcquisition",
      "Measure Evaluation Service": "MeasureEvaluation",
      "Normalization Service": "NormalizationService",
      "Query Dispatch Service": "QueryDispatch",
      "Report Service": "Report",
      "Submission Service": "Submission",
      "Tenant Service": "Tenant",
      "Validation Service": "Validation",
  }

  constructor(private auditService: AuditService, private tenantService: TenantService, private location: Location, private loadingService: LoadingService) { }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;

    this.loadingService.show();

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.auditService.searchLogs(null, null, null, null, null, null, null, this.defaultPageSize, this.defaultPageNumber)
        ]).subscribe({
          next: (response) => {
            this.facilityFilterOptions = response[0];
            this.auditLogs = response[1].records;
            this.paginationMetadata = response[1].metadata;
            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading audit logs:', error);
            this.loadingService.hide();
          }
        });
  }

  getAuditLogs(pageNumber: number = this.defaultPageNumber, pageSize: number = this.defaultPageSize) {

    let searchText = this.searchText ? this.searchText : null;
    let facility = this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null;
    let correlationId = this.correlationId ? this.correlationId : null;
    let service = this.selectedServiceFilter !== 'Any' ? this.selectedServiceFilter : null;
    let action = this.selectedActionFilter !== 'Any' ? this.selectedActionFilter : null;
    let user = this.user ? this.user : null;

    this.auditService.searchLogs(searchText, facility, correlationId, service, action,
      user, this.defaultSortBy, pageSize, pageNumber).subscribe(data => {
        this.auditLogs = data.records;
        this.paginationMetadata = data.metadata;
      });
  }

  expandedRows = new Set<string>();

  toggleRow(logId: string) {
    if (this.expandedRows.has(logId)) {
      this.expandedRows.delete(logId);
    } else {
      this.expandedRows.add(logId);
    }
  }

isRowExpanded(logId: string): boolean {
  return this.expandedRows.has(logId);
}

  pagedEvent(event: PageEvent) {
    this.getAuditLogs(event.pageIndex, event.pageSize);
  }

  onSearchTextChange(): void {
    this.getAuditLogs();
  }

  onCorrelationIdChange(): void {
    this.getAuditLogs();
  }

  onUserIdChange(): void {
    this.getAuditLogs();
  }

  onFacilityFilterChange(event: Event): void {
    this.getAuditLogs();
  }

  onServiceFilterChange(event: Event): void {
    this.getAuditLogs();
  }

  onActionFilterChange(event: Event): void {
    this.getAuditLogs();
  }

  clearCorrelationFilter() {
    this.correlationId = '';
    this.getAuditLogs();
  }

  clearFilters() {
    this.searchText = '';
    this.correlationId = '';
    this.user = '';
    this.selectedFacilityFilter = 'Any';
    this.selectedServiceFilter = 'Any';
    this.selectedActionFilter = 'Any';

    this.getAuditLogs();
  }

  onRefresh() {
    this.getAuditLogs();
  }

  navBack(): void {
    this.location.back();
  }

}
