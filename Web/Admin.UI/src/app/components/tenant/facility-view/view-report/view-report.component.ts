import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ValidationResultsComponent} from "../validation-results/validation-results.component";
import {CommonModule} from "@angular/common";

import {ActivatedRoute, RouterLink} from "@angular/router";
import { FacilityViewService } from '../facility-view.service';
import { IReportSummary } from '../report-view.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
@Component({
  selector: 'app-view-report',
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatTabsModule,
    RouterLink,
    ValidationResultsComponent    
  ],
  templateUrl: './view-report.component.html',
  styleUrl: './view-report.component.scss'
})
export class ViewReportComponent implements OnInit {
  facilityId: string = '';
  reportId: string = '';
  reportSummary: IReportSummary | undefined;

  facilityConfig: IFacilityConfigModel | undefined;
  scheduledReports: { cadence: string; measures: string[] }[] = []; // Array to hold scheduled reports

  constructor(
    private location: Location,
    private route: ActivatedRoute, 
    private tenantService: TenantService,
    private facilityViewService: FacilityViewService) { }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.reportId = params['reportId'];   
      this.loadFacilityConfig();
      this.loadReportSummary();  
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

  onRefresh(): void {
    this.loadReportSummary();
  }

  navBack(): void {
    this.location.back();
  }
}
