import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ValidationResultsComponent} from "../validation-results/validation-results.component";
import {CommonModule} from "@angular/common";

import {ActivatedRoute, Router, RouterLink, RouterLinkActive} from "@angular/router";
import { FacilityViewService } from '../facility-view.service';
import { IMeasureReportSummary, IReportListSummary } from '../report-view.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { ViewMeasureReportComponent } from '../view-measure-report/view-measure-report.component';
import { forkJoin, Subscription } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';

import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faRotate, faArrowLeft, faFileArrowDown, faFileInvoice, faSort, faSortUp, faSortDown } from '@fortawesome/free-solid-svg-icons';
import { LoadingService } from 'src/app/services/loading.service';
import { DonutChartComponent } from 'src/app/components/core/donut-chart/donut-chart.component';
import { ViewReportTableCommandComponent } from './table-command/view-report-table-command.component';
import { AcquisitionLogService } from '../../acquisition-log/acquisition-log.service';
import { IDataAcquisitionLogStatistics } from 'src/app/interfaces/data-acquisition/data-acquisition-log-statistics.interface';
import { ReportAnalysisComponent } from './report-analysis/report-analysis.component';

@Component({
  selector: 'app-view-report',
  imports: [
    CommonModule,
    FontAwesomeModule,
    FormsModule,
    MatDialogModule,
    MatToolbarModule,
    MatIconModule,
    MatPaginatorModule,
    MatTabsModule,
    MatButtonModule,
    MatTooltipModule,
    RouterLink,
    RouterLinkActive,
    ViewReportTableCommandComponent,
    ReportAnalysisComponent,
    DonutChartComponent
],
  templateUrl: './view-report.component.html',
  styleUrl: './view-report.component.scss'
})
export class ViewReportComponent implements OnInit {
  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFileArrowDown = faFileArrowDown;
  faFileInvoice = faFileInvoice;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;

  private subscription: Subscription | undefined;
  facilityId: string = '';
  reportId: string = '';

  reportSummary: IReportListSummary | undefined;
  dataAcquisitionLogStatistics: IDataAcquisitionLogStatistics | undefined;

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  measureReports: IMeasureReportSummary[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //filters
  patientFilter: string = '';
  reportFilter: string = '';
  selectedMeasureFilter: string = 'any';
  selectedReportStatusFilter: string = 'any';
  selectedValidationStatusFilter: string = 'any';
  measures: string[] = [];
  reportStatuses: string[] = [];
  validationStatuses: string[] = [];

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private router: Router,
    private dialog: MatDialog,
    private facilityViewService: FacilityViewService,
    private acquisitionLogService: AcquisitionLogService,
    private loadingService: LoadingService) { }

  ngOnInit(): void {
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.reportId = params['reportId'];

      this.loadingService.show();

      forkJoin([
          this.facilityViewService.getReportSummary(this.facilityId, this.reportId),
          this.facilityViewService.getMeasureReportSummaryList(this.facilityId, this.reportId, null, null, null, null, null, null, null, this.defaultPageNumber, this.defaultPageSize),
          this.facilityViewService.getReportSubmissionStatuses(),
          this.facilityViewService.getReportValidationStatuses()
        ]).subscribe({
          next: (response) => {
            this.reportSummary = response[0];
            this.measureReports = response[1].records;
            this.paginationMetadata = response[1].metadata;
            this.measures = this.reportSummary.reportTypes;
            this.reportStatuses = response[2];
            this.validationStatuses = response[3];
            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading report summary:', error);
            this.loadingService.hide();
          }
        });
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

    this.facilityViewService.getMeasureReportSummaryList(
        this.facilityId, 
        this.reportId, 
        this.patientFilter.length > 0 ? this.patientFilter : null, 
        this.reportFilter.length > 0 ? this.reportFilter : null,
        this.selectedMeasureFilter === 'any' ? null : this.selectedMeasureFilter, 
        this.selectedReportStatusFilter === 'any' ? null : this.selectedReportStatusFilter, 
        this.selectedValidationStatusFilter === 'any' ? null : this.selectedValidationStatusFilter,
        this.sortBy, 
        this.sortOrder,
        pageNumber, 
        pageSize).subscribe({
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
    dialogConfig.minWidth = '90vw';
    dialogConfig.maxHeight = '85vh';
    dialogConfig.panelClass = 'link-dialog-container';
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

  onDownload() {
    this.facilityViewService.downloadReport(this.facilityId, this.reportId);
  }

  onPatientIdChange(): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  onReportIdChange(): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  onMeasureFilterChange(event: Event): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  onReportStatusFilterChange(event: Event): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  onValidationStatusFilterChange(event: Event): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  clearFilters(): void {
    this.patientFilter = '';
    this.reportFilter = '';
    this.selectedMeasureFilter = 'any';
    this.selectedReportStatusFilter = 'any';
    this.selectedValidationStatusFilter = 'any';
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
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

    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  getSortIcon(column: string) {
    if (this.sortBy !== column) return this.faSort;
    if (this.sortOrder === 'ascending') return this.faSortUp;
    if (this.sortOrder === 'descending') return this.faSortDown;

    return this.faSort;
  }

  onRefresh(): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  onViewAcquisitionLog() {

    const urlTree = this.router.createUrlTree(['/tenant/acquisition-log'], {
      queryParams: { reportId: this.reportId }
    });
    const fullUrl = this.router.serializeUrl(urlTree);

    window.open(fullUrl, '_blank');
    //this.router.navigate(['tenant/acquisition-log'], { queryParams: { reportId: this.reportId } });
  }

  onViewQueryAnalysis() {
    this.loadingService.show();
    this.acquisitionLogService.getAcquisitionLogStatistics(this.reportId).subscribe({
      next: (response: IDataAcquisitionLogStatistics) => {
        this.dataAcquisitionLogStatistics = response;
        this.loadingService.hide();
      },
      error: (error) => {
        console.error('Error loading acquisition log statistics:', error);
        this.loadingService.hide();
      }
    });

  }

  onTabSelected(event: any): void {
    if (event === 1) {
      this.onViewQueryAnalysis();
    }
  }

  navBack(): void {
    this.location.back();
  }
}
