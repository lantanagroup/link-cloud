import {Component, Inject, OnInit} from '@angular/core';
import {FacilityViewService} from '../facility-view.service';
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
import {MatSnackBar} from "@angular/material/snack-bar";
import { IReportEntry, ReportingStatus, SubmissionStatus } from '../../../../interfaces/report/report-entry.interface';

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
  measureReport!: IReportEntry;

  resourceTypes: string[] = [];
  selectedResourceType: string = 'any';

  constructor(
    public dialogRef: MatDialogRef<ViewMeasureReportComponent>,
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      facilityId: string,
      measureReport: IReportEntry
    },
    private fileService: FileDownloadService, private appConfigService: AppConfigService, private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.facilityId = this.data.facilityId;
    this.measureReport = this.data.measureReport;
  }

  onModalClose(): void {
    this.dialogRef.close();
  }

  getAggregatedResourceCount(): Record<string, number> {
    const aggregated: Record<string, number> = {};
    
    if (!this.measureReport?.measureReports) {
      return aggregated;
    }

    for (const report of this.measureReport.measureReports) {
      if (report.resourceCount) {
        for (const [resourceType, count] of Object.entries(report.resourceCount)) {
          aggregated[resourceType] = (aggregated[resourceType] || 0) + count;
        }
      }
    }

    return aggregated;
  }

  downloadReport() {
    this.fileService.downloadFileFromJson(`${this.appConfigService.config?.baseApiUrl}/measureeval/patient/${this.facilityId}/${this.measureReport?.reportScheduleId}/${this.measureReport?.patientId}`)
      .subscribe({
        next: () => console.log('Download started'),
        error: (error: HttpErrorResponse) => {
          console.error('Error downloading patient bundle:', error.message);
          if (error.status === 404) {
            this.snackBar.open(`Error downloading patient bundle. Bundle not found.`, '', {
              duration: 3500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
          else{
            this.snackBar.open(`Error downloading patient bundle.`, '', {
              duration: 3500,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        }
      });
  }

  getReportingStatusText(status: ReportingStatus | undefined): string {
    switch (status) {
      case ReportingStatus.PatientIdentified:
        return 'Patient Identified';
      case ReportingStatus.NotReportable:
        return 'Not Reportable';
      case ReportingStatus.PendingValidation:
        return 'Pending Validation';
      case ReportingStatus.PassedValidation:
        return 'Passed Validation';
      case ReportingStatus.FailedValidation:
        return 'Failed Validation';
      default:
        return '';
    }
  }

  getSubmissionStatusText(status: SubmissionStatus | undefined): string {
    switch (status) {
      case SubmissionStatus.PendingValidation:
        return 'Pending Validation';
      case SubmissionStatus.Submitting:
        return 'Submitting';
      case SubmissionStatus.Submitted:
        return 'Submitted';
      case SubmissionStatus.FailedSubmission:
        return 'Failed Submission';
      case SubmissionStatus.NotEligable:
        return 'Not Eligible';
      default:
        return '';
    }
  }

  protected readonly faDownload = faDownload;
}


