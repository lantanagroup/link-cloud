import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { FacilityConfigFormComponent } from '../facility-config-form/facility-config-form.component';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FacilityConfigDialogComponent } from '../facility-config-dialog/facility-config-dialog.component';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatAccordion, MatExpansionModule } from '@angular/material/expansion';
import { MatTabsModule } from '@angular/material/tabs';
import { CensusConfigDialogComponent } from '../../census/census-config-dialog/census-config-dialog.component';
import { CensusService } from 'src/app/services/gateway/census/census.service';
import { ICensusConfiguration } from 'src/app/interfaces/census/census-config-model.interface';


@Component({
  selector: 'app-facility-view',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    RouterLink,   
    MatDialogModule,
    MatExpansionModule,
    MatTabsModule,
    FacilityConfigFormComponent,
    CensusConfigDialogComponent
  ],
  templateUrl: './facility-view.component.html',
  styleUrls: ['./facility-view.component.scss']
})
export class FacilityViewComponent implements OnInit {
  @ViewChild(MatAccordion) accordion!: MatAccordion;

  facilityId: string = '';
  facilityConfig!: IFacilityConfigModel;
  facilityConfigFormViewOnly: boolean = true;
  facilityConfigFormIsInvalid: boolean = false;

  censusConfig!: ICensusConfiguration;

  constructor(
    private route: ActivatedRoute, 
    private tenantService: TenantService, 
    private censusService: CensusService,
    private dialog: MatDialog, 
    private snackBar: MatSnackBar) { }

  ngOnInit() {
    this.route.params.subscribe(params => {
      this.facilityId = params['id'];
      this.loadFacilityConfig();
    });    
  }    

  showFacilityDialog(): void {
    this.dialog.open(FacilityConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Edit facility', viewOnly: false, facilityConfig: this.facilityConfig }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {        
          this.loadFacilityConfig();  
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  //load facility configurations
  loadFacilityConfig(): void { 
    this.tenantService.getFacilityConfiguration(this.facilityId).subscribe((data: IFacilityConfigModel) => {
      this.facilityConfig = data;
    }); 
  }

  loadCensusConfig(): void { 
    if(!this.censusConfig) {
      this.censusService.getConfiguration(this.facilityId).subscribe((data: ICensusConfiguration) => {
        this.censusConfig = data;
      }, error => {
        if(error.status == 404) {
          this.censusConfig = { facilityId: this.facilityConfig.facilityId, scheduledTrigger: ''} as ICensusConfiguration;
        }
        else {
          this.snackBar.open(`Failed to load census configuration for the facility, see error for details.`, '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      }); 
    }
  }
  

}