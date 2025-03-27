import { Component, Inject, OnInit } from '@angular/core';
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary, IResourceSummary } from '../report-view.interface';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ResourcePieChartComponent } from '../resource-pie-chart/resource-pie-chart.component';
import { forkJoin } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-view-measure-report',
  imports: [
    CommonModule,
    FormsModule,
    MatPaginatorModule,
    ResourcePieChartComponent
  ],
  templateUrl: './view-measure-report.component.html',
  styleUrl: './view-measure-report.component.scss'
})
export class ViewMeasureReportComponent implements OnInit {
  facilityId: string = '';
  measureReport!: IMeasureReportSummary;

  defaultPageNumber: number = 0;
  defaultPageSize: number = 10;
  resources: IResourceSummary[] = [];  
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  resourceTypes: string[] = [];
  selectedResourceType: string = 'any';

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, facilityId: string, measureReport: IMeasureReportSummary },
    private facilityViewService: FacilityViewService) { }
  
  ngOnInit(): void {
    this.facilityId = this.data.facilityId;
    this.measureReport = this.data.measureReport;
    if (this.measureReport) {
      forkJoin({
        summary: this.facilityViewService.getMeasureReportResourceDetails(this.facilityId, this.measureReport.id, null, this.defaultPageNumber, this.defaultPageSize),
        resourceTypes: this.facilityViewService.getMeasureReportResourceTypes(this.facilityId, this.measureReport.id)
      }).subscribe({
        next: ({ summary, resourceTypes }) => {
          this.resources = summary.records;
          this.paginationMetadata = summary.metadata;
          this.resourceTypes = resourceTypes;          
        },
        error: (error: HttpErrorResponse) => {
          console.error('Error loading measure report data:', error.message);
        }
      });
    }
  }
  
  loadMeasureReportSummary(pageNumber: number, pageSize: number): void {
    let resourceType: string | null = this.selectedResourceType === 'any' ? null : this.selectedResourceType;
    this.facilityViewService.getMeasureReportResourceDetails(this.facilityId, this.measureReport?.id, resourceType, pageNumber, pageSize).subscribe({
      next: (response) => {
        this.resources = response.records;
        this.paginationMetadata = response.metadata;
      },
      error: (error: HttpErrorResponse) => {
        console.error('Error loading measure report summary:', error.message);
      }
    });
  }

  loadMeasureReportResourceTypes(): void {
    this.facilityViewService.getMeasureReportResourceTypes(this.facilityId, this.measureReport?.id).subscribe({
      next: (response: string[]) => {
        this.resourceTypes = response;
      },
      error: (error: HttpErrorResponse) => {
        console.error('Error loading measure report resource types:', error.message);
      }
    });
  }

  onResourceTypeChange(event: Event): void {    
    this.loadMeasureReportSummary(this.defaultPageNumber, this.defaultPageSize);
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadMeasureReportSummary(event.pageIndex, event.pageSize);
  }

  onRefresh(): void {
    this.loadMeasureReportSummary(this.defaultPageNumber, this.defaultPageSize);
  }

}

  
