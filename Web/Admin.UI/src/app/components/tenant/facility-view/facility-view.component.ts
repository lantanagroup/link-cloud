import {Component, Input, OnInit} from '@angular/core';
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {ActivatedRoute, RouterLink} from "@angular/router";
import {MatCardModule} from "@angular/material/card";
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { FacilityViewService } from './facility-view.service';
import { IPagedReportListSummary, IReportListSummary } from './report-list-summary.interface';
import { CommonModule } from '@angular/common';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';

@Component({
  selector: 'app-facility-view',
  imports: [
    CommonModule,
    MatTableModule,
    RouterLink,
    MatCardModule
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.css'
})
export class FacilityViewComponent implements OnInit {
  dataSource!: MatTableDataSource<IReportListSummary>;
  displayedColumns: string[] = ['id', 'period', 'plan', 'cadence', 'measures', 'census', 'ip']; 

  facilityId: string = '';
  facilityConfig: IFacilityConfigModel | undefined;
 
  defaultPageNumber: number = 1;
  defaultPageSize: number = 10;
  reportListSummary: IReportListSummary[] = [];  
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  constructor(
    private route: ActivatedRoute, 
    private tenantService: TenantService,
    private facilityViewService: FacilityViewService) { }  
 
  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<IReportListSummary>();
    
    this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.loadFacilityConfig();
      this.loadReportSummaryList(this.defaultPageNumber, this.defaultPageSize);
    });
  }

   loadFacilityConfig(): void {
      this.tenantService.getFacilityConfiguration(this.facilityId).subscribe((data: IFacilityConfigModel) => {
        this.facilityConfig = data;
      });
    }

    loadReportSummaryList(pageNumber: number, pageSize: number): void {
      this.facilityViewService.getReportSummaryList(this.facilityId, pageNumber, pageSize).subscribe({
        next: (response: IPagedReportListSummary) => {
          this.reportListSummary = response.records;
          this.paginationMetadata = response.metadata;
          this.dataSource.data = this.reportListSummary;
          //this.initializeSummaries = false;
        },
        error: (error) => {
          console.error('Error fetching facility report summaries:', error);
        }
      });   
             
    }

    onRefresh(pageNumber: number, pageSize: number): void {
      this.loadReportSummaryList(this.paginationMetadata.pageNumber, this.paginationMetadata.pageSize);
    }

}
