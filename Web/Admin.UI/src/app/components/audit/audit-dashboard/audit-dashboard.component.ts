import { Component, OnInit } from '@angular/core';
import { animate, state, style, transition, trigger } from '@angular/animations';
import { CommonModule } from '@angular/common';
import { AuditService } from '../../../services/gateway/audit.service';
import { AuditModel } from '../../../models/audit/audit-model.model';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSelectChange, MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ITableHeaderModel } from '../../../interfaces/table-header-model.interface';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { PagedFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';

@Component({
  selector: 'app-audit-dashboard',
  standalone: true,
  imports: [
    CommonModule,
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
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  auditLogs: AuditModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  displayedColumns: ITableHeaderModel[] = [{ key: 'id', display: 'Id' }, { key: 'facilityId', display: 'Facility' }, { key: 'correlationId', display: 'Correlation Id' }, { key: 'serviceName', display: 'Service Name' }, { key: 'action', display: 'Action' }, { key: 'eventDate', display: 'Date' }];
  columnsToDisplayWithExpand = [...this.displayedColumns.map(x => x.key), 'expand'];
  expandedRecord: AuditModel | null | undefined;
  dataSource = new MatTableDataSource<AuditModel>(this.auditLogs);

  //search parameters
  searchText: string = '';
  filterFacilityBy: string = '';
  filterCorrelationBy: string = '';
  filterServiceBy: string = '';
  filterActionBy: string = '';
  filterUserBy: string = '';
  sortBy: string = '';

  //filters
  facilityFilter: string[] = [];
  serviceFilter = ["All", "Account Service", "Census Service", "Data Acquisition", "Notification Service", "NormalizationService", "Tenant"];
  actionFilter = ["All", "Create", "Update", "Delete", "Query", "Submission"];

  constructor(private auditService: AuditService, private tenantService: TenantService) { }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getAuditLogs();
    this.facilityFilter = this.loadFacilityFilter();
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getAuditLogs();
  }

  getAuditLogs() {
    this.auditService.list(this.searchText, this.filterFacilityBy, this.filterCorrelationBy, this.filterServiceBy, this.filterActionBy,
      this.filterUserBy, this.sortBy, this.paginationMetadata.pageSize, this.paginationMetadata.pageNumber).subscribe(data => {
        this.auditLogs = data.records;
        this.paginationMetadata = data.metadata;
      });
  }

  onFilterSelection(filter: MatSelectChange) {

    switch (filter.source.id) {
      case ("facilityFilterSelector"):
        {
          if (filter.value === 'All') {
            this.filterFacilityBy = '';
          }
          else {
            this.filterFacilityBy = filter.value;
          }
        }
        break;
      case ("serviceFilterSelector"):
        {
          if (filter.value === 'All') {
            this.filterServiceBy = '';
          }
          else {
            this.filterServiceBy = filter.value;
          }
        }
        break;
      case ("actionFilterSelector"):
        {
          if (filter.value === 'All') {
            this.filterActionBy = '';
          }
          else {
            this.filterActionBy = filter.value;
          }
        }
        break;
    }

    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.getAuditLogs();
  }

  applyCorrelationFilter() {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.getAuditLogs();
  }

  clearCorrelationFilter() {
    this.filterCorrelationBy = '';
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.getAuditLogs();
  }

  clearFilters() {
    this.searchText = '';
    this.filterFacilityBy = '';
    this.filterCorrelationBy = '';
    this.filterServiceBy = '';
    this.filterActionBy = '';
    this.filterUserBy = '';
    this.sortBy = '';

    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.getAuditLogs();
  }

  loadFacilityFilter() : string[] {
    var facilityList: string[] = ["All"];
    this.tenantService.listFacilities('', '').subscribe((facilities: PagedFacilityConfigModel) => {
          facilities.records.map(facility => facility.facilityId).forEach(facilityId => {
            facilityList.push(facilityId);
          });
        });
    return facilityList;
  }

}
