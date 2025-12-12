import {Component, Inject, OnInit} from '@angular/core';
import {FacilityViewService} from '../facility-view.service';
import {IMeasureReportSummary, IResourceSummary} from '../report-view.interface';
import {PaginationMetadata} from 'src/app/models/pagination-metadata.model';
import {MatPaginatorModule, PageEvent} from '@angular/material/paginator';

import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {forkJoin} from 'rxjs';
import {HttpErrorResponse} from '@angular/common/http';
import {FormsModule} from '@angular/forms';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {faDownload, faXmark} from '@fortawesome/free-solid-svg-icons';
import {DonutChartComponent} from 'src/app/components/core/donut-chart/donut-chart.component';
import {FileDownloadService} from "../../../core/file-downlaod/file-download.service";
import {AppConfigService} from "../../../../services/app-config.service";

@Component({
  selector: 'app-view-measure-report',
  imports: [
    FontAwesomeModule,
    FormsModule,
    MatPaginatorModule,
    DonutChartComponent
  ],
  templateUrl: './view-measure-report.component.html',
  styleUrl: './view-measure-report.component.scss'
})
export class ViewMeasureReportComponent implements OnInit {
  faXmark = faXmark;

  title: string = '';
  facilityId: string = '';
  measureReport!: IMeasureReportSummary;

  defaultPageNumber: number = 0;
  defaultPageSize: number = 10;
  resources: IResourceSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  resourceTypes: string[] = [];
  selectedResourceType: string = 'any';

  constructor(
    public dialogRef: MatDialogRef<ViewMeasureReportComponent>,
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      facilityId: string,
      measureReport: IMeasureReportSummary
    },
    private facilityViewService: FacilityViewService, private fileService: FileDownloadService, private appConfigService: AppConfigService) {
  }

  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.facilityId = this.data.facilityId;
    this.measureReport = this.data.measureReport;

    if (this.measureReport) {
      forkJoin({
        summary: this.facilityViewService.getMeasureReportResourceDetails(this.facilityId, this.measureReport.id, null, this.defaultPageNumber, this.defaultPageSize),
        resourceTypes: this.facilityViewService.getMeasureReportResourceTypes(this.facilityId, this.measureReport.id)
      }).subscribe({
        next: ({summary, resourceTypes}) => {
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

  onModalClose(): void {
    this.dialogRef.close();
  }

  downloadReport() {
    this.fileService.downloadFileFromJson(`${this.appConfigService.config?.baseApiUrl}/measureeval/patient/${this.facilityId}/${this.measureReport?.reportScheduleId}/${this.measureReport?.patientId}`)
      .subscribe({
        next: () => console.log('Download started'),
        error: (error: HttpErrorResponse) => {
          console.error('Error downloading patient bundle:', error.message);
        }
      });
  }

  protected readonly faDownload = faDownload;
}


