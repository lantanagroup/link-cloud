import { Component, Inject, OnInit } from '@angular/core';
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary, IResourceSummary } from '../report-view.interface';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';

@Component({
  selector: 'app-view-measure-report',
  imports: [
    CommonModule,
    MatPaginatorModule
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

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, facilityId: string, measureReport: IMeasureReportSummary },
    private facilityViewService: FacilityViewService) { }
  
  ngOnInit(): void {
    this.facilityId = this.data.facilityId;
    this.measureReport = this.data.measureReport;
    if (this.measureReport) {
      this.loadMeasureReportSummary(this.defaultPageNumber, this.defaultPageSize);
    }
  }
  
  loadMeasureReportSummary(pageNumber: number, pageSize: number): void {
    this.facilityViewService.getMeasureReportResourceDetails(this.facilityId, this.measureReport?.id, pageNumber, pageSize).subscribe({
      next: (response) => {
        this.resources = response.records;
        this.paginationMetadata = response.metadata;
      },
      error: (error) => {
        console.error('Error loading measure report summary:', error);
      }
    });
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

  
