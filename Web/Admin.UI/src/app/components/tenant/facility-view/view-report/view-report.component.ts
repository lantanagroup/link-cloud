import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ValidationResultsComponent} from "../validation-results/validation-results.component";
import {CommonModule} from "@angular/common";

import {ActivatedRoute, RouterLink} from "@angular/router";
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary, IReportListSummary } from '../report-view.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { ViewMeasureReportComponent } from '../view-measure-report/view-measure-report.component';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-view-report',
  imports: [
    CommonModule,
    MatDialogModule,
    MatToolbarModule,
    MatIconModule,
    MatPaginatorModule,
    MatTabsModule,
    RouterLink,
    ValidationResultsComponent    
  ],
  templateUrl: './view-report.component.html',
  styleUrl: './view-report.component.scss'
})
export class ViewReportComponent implements OnInit {
  private subscription: Subscription | undefined;
  facilityId: string = '';
  reportId: string = '';

  reportSummary: IReportListSummary | undefined;

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  measureReports: IMeasureReportSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  
  constructor(
    private location: Location,
    private route: ActivatedRoute, 
    private tenantService: TenantService,
    private dialog: MatDialog,
    private facilityViewService: FacilityViewService) { }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.reportId = params['reportId'];   
      this.loadReportSummary();  
      this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
        this.subscription.unsubscribe();
    }
  }

  loadReportSummary(): void {
    this.facilityViewService.getReportSummary(this.facilityId, this.reportId).subscribe({
      next: (response) => {
        this.reportSummary = response;
      },
      error: (error) => {
        console.error('Error loading report summary:', error);
      }
    });
  }

  loadMeasureReports(pageNumber: number, pageSize: number): void {
    this.facilityViewService.getMeasureReportSummaryList(this.facilityId, this.reportId, pageNumber, pageSize).subscribe({
      next: (response) => {
        this.measureReports = response.records;
        this.paginationMetadata = response.metadata;
      },
      error: (error) => {
        console.error('Error loading measure reports:', error);
      }
    });
  }

  onSelectReport(measureReport: IMeasureReportSummary): void {

    

    const dialogConfig = new MatDialogConfig();
    dialogConfig.minWidth = '75vw'; 
    dialogConfig.data = {
      dialogTitle: 'Measure Report Details',
      viewOnly: false,
      facilityId: this.facilityId,
      measureReport: measureReport
    };
    
      this.dialog.open(ViewMeasureReportComponent, dialogConfig);
    }

  pagedEvent(event: PageEvent) {
      this.paginationMetadata.pageSize = event.pageSize;
      this.paginationMetadata.pageNumber = event.pageIndex;
      this.loadMeasureReports(event.pageIndex, event.pageSize);
    }

  onRefresh(): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  navBack(): void {
    this.location.back();
  }
}
