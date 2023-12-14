import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';


@Component({
  selector: 'app-facility-view',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    RouterLink
  ],
  templateUrl: './facility-view.component.html',
  styleUrls: ['./facility-view.component.scss']
})
export class FacilityViewComponent implements OnInit {
  facilityId: string = '';
  facilityConfig: IFacilityConfigModel | null = null;

  constructor(private route: ActivatedRoute, private tenantService: TenantService) { }

  ngOnInit() {
    this.route.params.subscribe(params => {
      this.facilityId = params['id'];
      this.tenantService.getFacilityConfiguration(this.facilityId).subscribe((data: IFacilityConfigModel) => {
        this.facilityConfig = data;
      }); 
    });    
  }

}