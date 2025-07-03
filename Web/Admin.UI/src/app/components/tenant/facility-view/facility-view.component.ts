import {Component, OnInit} from '@angular/core';
import { Location } from '@angular/common';
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
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
import { MatButtonModule } from '@angular/material/button';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotate, faArrowLeft, faGears } from '@fortawesome/free-solid-svg-icons';
import { LoadingService } from 'src/app/services/loading.service';
import { forkJoin, Subscription } from 'rxjs';

@Component({
  selector: 'app-facility-view',
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    RouterLink,
    MatCardModule
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.scss'
})
export class FacilityViewComponent implements OnInit {
  private subscription: Subscription | undefined;

  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faGears = faGears;

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
    private router: Router,
    private tenantService: TenantService,
    private facilityViewService: FacilityViewService,
    private loadingService: LoadingService) { }

  ngOnInit(): void {

    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];

      this.loadingService.show();

      forkJoin([
          this.tenantService.getFacilityConfiguration(this.facilityId),
          this.facilityViewService.getReportSummaryList(this.facilityId, this.defaultPageNumber, this.defaultPageSize)
        ]).subscribe({
          next: (response) => {
            this.facilityConfig = response[0];

            this.scheduledReports = this.facilityConfig?.scheduledReports ? [
              { cadence: 'Daily', measures: this.facilityConfig.scheduledReports.daily },
              { cadence: 'Weekly', measures: this.facilityConfig.scheduledReports.weekly },
              { cadence: 'Monthly', measures: this.facilityConfig.scheduledReports.monthly }
            ] : [];

            this.reportListSummary = response[1].records;
            this.paginationMetadata = response[1].metadata;

            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading report summaries:', error);
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

    onFacilityConfig(): void {
      this.router.navigate(['/tenant/facility', this.facilityId, 'edit']);
    }

    navBack(): void {
      this.location.back();
    }

}
