import { ActivatedRoute, RouterLink } from '@angular/router';
import { Component, OnDestroy, OnInit } from '@angular/core';


import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { Subscription } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';

@Component({
  selector: 'app-sub-pre-qual-report-banner',
  imports: [],
  templateUrl: './sub-pre-qual-report-banner.component.html',
  styleUrls: ['./sub-pre-qual-report-banner.component.scss'],
  standalone: true
})
export class SubPreQualReportBannerComponent implements OnInit, OnDestroy {
  private subscription: Subscription | undefined;

  facilityId: string = '';
  submissionId: string = '';
  facilityName?: string;
  facilityConfig: IFacilityConfigModel | undefined;

  loading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private tenantService: TenantService
  ) { }

  ngOnInit(): void {
    this.loading = true;
    this.error = null;

    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
    });

    this.tenantService.getFacilityConfiguration(this.facilityId).subscribe({
      next: (response) => {
        this.facilityConfig = response;
        this.facilityName = this.facilityConfig?.facilityName;
      },
      error: (error) => {
        this.error = 'Failed to load facility configuration.';
        this.loading = false;
        console.error('Error loading facility configuration:', error);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
