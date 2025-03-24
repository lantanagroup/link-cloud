import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ActivatedRoute, RouterLink} from "@angular/router";
import {MatCardModule} from "@angular/material/card";
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { FacilityViewService } from './facility-view.service';
import { IPagedReportListSummary, IReportListSummary } from './report-view.interface';
import { CommonModule } from '@angular/common';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';

@Component({
  selector: 'app-facility-view',
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatPaginatorModule,
    RouterLink,
    MatCardModule
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.scss'
})
export class FacilityViewComponent implements OnInit {
 
  facilityId: string = '';
  facilityConfig: IFacilityConfigModel | undefined;
  scheduledReports: { cadence: string; measures: string[] }[] = []; // Array to hold scheduled reports
 
  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  reportListSummary: IReportListSummary[] = [];  
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  constructor(
    private location: Location,
    private route: ActivatedRoute, 
    private tenantService: TenantService,
    private facilityViewService: FacilityViewService) { }  
 
  ngOnInit(): void {   
    
    this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.loadFacilityConfig();
      this.loadReportSummaryList(this.defaultPageNumber, this.defaultPageSize);
    });
  }

   loadFacilityConfig(): void {
      this.tenantService.getFacilityConfiguration(this.facilityId).subscribe({
          next: (response: IFacilityConfigModel) => {
            this.facilityConfig = response;

            this.scheduledReports = this.facilityConfig?.scheduledReports ? [
              { cadence: 'Daily', measures: this.facilityConfig.scheduledReports.daily },
              { cadence: 'Weekly', measures: this.facilityConfig.scheduledReports.weekly },
              { cadence: 'Monthly', measures: this.facilityConfig.scheduledReports.monthly }
            ] : []
          },
          error: (error) => {
            console.error('Error fetching facility configuration:', error);
          }
        });            
    }

    loadReportSummaryList(pageNumber: number, pageSize: number): void {
      this.facilityViewService.getReportSummaryList(this.facilityId, pageNumber, pageSize).subscribe({
        next: (response: IPagedReportListSummary) => {
          this.reportListSummary = response.records;
          this.paginationMetadata = response.metadata;
        },
        error: (error) => {
          console.error('Error fetching facility report summaries:', error);
        }
      });                
    }

    pagedEvent(event: PageEvent) {
      this.paginationMetadata.pageSize = event.pageSize;
      this.paginationMetadata.pageNumber = event.pageIndex;
      this.loadReportSummaryList(event.pageIndex, event.pageSize);
    }

    onRefresh(): void {
      this.loadReportSummaryList(this.defaultPageNumber, this.defaultPageSize);
    }

    navBack(): void {
      this.location.back();
    }

}
