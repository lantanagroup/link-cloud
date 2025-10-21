import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {Location} from '@angular/common';
import {ActivatedRoute, Router, RouterLink} from "@angular/router";
import {MatCardModule} from "@angular/material/card";
import {TenantService} from 'src/app/services/gateway/tenant/tenant.service';
import {IFacilityConfigModel} from 'src/app/interfaces/tenant/facility-config-model.interface';
import {FacilityViewService} from './facility-view.service';
import {CommonModule} from '@angular/common';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import { FormsModule } from "@angular/forms";
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatTabsModule} from '@angular/material/tabs';
import {LocationsListComponent} from './locations-list/locations-list.component';
import {EncountersListComponent} from './encounters-list/encounters-list.component';
import {FacilityReportingPlansComponent} from './facility-reporting-plans/facility-reporting-plans.component';
import {AppConfigService} from '../../../services/app-config.service';
import {faArrowLeft, faGears} from '@fortawesome/free-solid-svg-icons';
import {forkJoin} from 'rxjs';
import {MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTableModule} from '@angular/material/table';
import {MatSortModule} from '@angular/material/sort';
import {MatPaginatorModule} from '@angular/material/paginator';
import {ReportScheduleGridBase} from '../../reports/report-schedule-grid.base';

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
    MatCardModule,
    FormsModule,
    MatTableModule,
    MatSortModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatCheckbox,
    MatTooltipModule,
    MatTabsModule,
    LocationsListComponent,
    EncountersListComponent,
    FacilityReportingPlansComponent
  ],
  templateUrl: './facility-view.component.html',
  styleUrl: './facility-view.component.scss'
})
export class FacilityViewComponent extends ReportScheduleGridBase implements OnInit, OnDestroy {
  faArrowLeft = faArrowLeft;
  faGears = faGears;

  facilityId: string = '';
  facilityConfig: IFacilityConfigModel | undefined;
  scheduledReports: { cadence: string; measures: string[] }[] = [];

  // Fixed for this page: the grid only ever shows one facility's reports.
  protected get facilityFilter(): string | undefined {
    return this.facilityId;
  }

  constructor(
    private location: Location,
    private route: ActivatedRoute,
    private router: Router,
    private facilityViewService: FacilityViewService,
    private appConfigService: AppConfigService) {
    super();
  }

  /**
   * DMRP feature flag, mirroring the nav bar's gating: the Reporting Plans tab only exists while
   * the module is on, because its api/dmrp routes do not exist when it is off.
   */
  get dmrpEnabled(): boolean {
    return this.appConfigService.config?.dmrpEnabled ?? false;
  }

  ngOnInit(): void {
    this.initPagination();
    this.watchReportIdFilter();

    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];

      this.loadingService.show();

      forkJoin([
        this.tenantService.getFacilityConfiguration(this.facilityId)
      ]).subscribe({
        next: (response) => {
          this.facilityConfig = response[0];
          this.scheduledReports = this.buildScheduledReports();
          this.loadReportSchedules();
          this.loadingService.hide();
        },
        error: (error) => {
          console.error('Error loading report summaries:', error);
          this.loadingService.hide();
        }
      });
    });
  }

  loadFacilityConfig(): void {
    this.tenantService.getFacilityConfiguration(this.facilityId).subscribe({
      next: (response: IFacilityConfigModel) => {
        this.facilityConfig = response;
        this.scheduledReports = this.buildScheduledReports();
      },
      error: (error) => {
        console.error('Error fetching facility configuration:', error);
      }
    });
  }

  private buildScheduledReports(): { cadence: string; measures: string[] }[] {
    if (!this.facilityConfig?.scheduledReports) {
      return [];
    }
    return [
      { cadence: 'Daily', measures: this.facilityConfig.scheduledReports.daily },
      { cadence: 'Weekly', measures: this.facilityConfig.scheduledReports.weekly },
      { cadence: 'Monthly', measures: this.facilityConfig.scheduledReports.monthly }
    ];
  }

  onFacilityConfig(): void {
    this.router.navigate(['/tenant/facility', this.facilityId, 'edit']);
  }

  navBack(): void {
    this.location.back();
  }
}
