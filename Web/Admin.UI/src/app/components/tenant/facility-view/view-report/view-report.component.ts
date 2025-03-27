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
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-view-report',
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatToolbarModule,
    MatIconModule,
    MatPaginatorModule,
    MatTabsModule,
    MatButtonModule,
    MatTooltipModule,
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

  //filters
  patientFilter: string = '';
  reportFilter: string = '';
  selectedMeasureFilter: string = 'any';
  selectedReportStatusFilter: string = 'any';
  selectedValidationStatusFilter: string = 'any';

  measures: string[] = [
    "NHSNdQMAcuteCareHospitalInitialPopulation",
    "NHSNGlycemicControlHypoglycemicInitialPopulation"
  ];
  
  reportStatuses: string[] = [
    "PendingEvaluation",
    "NotReportable",
    "ReadyForValidation",
    "ValidationRequested",
    "ValidationComplete",
    "Submitted"
  ]

  validationStatuses: string[] = [
    "Pending",
    "Passed",
    "Failed",
    "Requested"
  ]

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

    let patientId: string | null = this.patientFilter.length > 0 ? this.patientFilter : null;
    let measureReportId: string | null = this.reportFilter.length > 0 ? this.reportFilter : null;
    let measure: string | null = this.selectedMeasureFilter === 'any' ? null : this.selectedMeasureFilter;
    let reportStatus: string | null = this.selectedReportStatusFilter === 'any' ? null : this.selectedReportStatusFilter;
    let validationStatus: string | null = this.selectedValidationStatusFilter === 'any' ? null : this.selectedValidationStatusFilter;

    this.facilityViewService.getMeasureReportSummaryList(this.facilityId, this.reportId, patientId, measureReportId, 
      measure, reportStatus, validationStatus, pageNumber, pageSize).subscribe({
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
    dialogConfig.maxHeight = '75vh';
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

  onRefresh(): void {
    this.loadMeasureReports(this.defaultPageNumber, this.defaultPageSize);
  }

  navBack(): void {
    this.location.back();
  }
}
