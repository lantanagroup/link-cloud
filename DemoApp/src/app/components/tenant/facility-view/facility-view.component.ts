import { Component, OnInit, Input, ViewChild } from '@angular/core';
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
import { CensusConfigFormComponent } from "../../census/census-config-form/census-config-form.component";
import { LinkAlertComponent } from "../../core/link-alert/link-alert.component";
import { LinkAlertType } from '../../core/link-alert/link-alert-type.enum';
import { FormMode } from 'src/app/models/FormMode.enum';
import { ReportConfigFormComponent } from '../../report/report-config-form/report-config-form.component';
import { ReportService } from '../../../services/gateway/report/report.service';
import { ReportDashboardComponent } from '../../report/report-dashboard/report-dashboard.component';

@Component({
    selector: 'app-facility-view',
    standalone: true,
    templateUrl: './facility-view.component.html',
    styleUrls: ['./facility-view.component.scss'],
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
        CensusConfigFormComponent,
        LinkAlertComponent,
        ReportConfigFormComponent,
        ReportDashboardComponent
    ]
})
export class FacilityViewComponent implements OnInit {
  @ViewChild(MatAccordion) accordion!: MatAccordion;

  facilityId: string = '';
  facilityConfig!: IFacilityConfigModel;
  facilityConfigFormViewOnly: boolean = true;
  facilityConfigFormIsInvalid: boolean = false;

  censusConfig!: ICensusConfiguration;
  linkNoConfigAlertType = LinkAlertType.info;
  showNoCensusConfigAlert: boolean = false;
  noCensusConfigAlertMessage = 'No census configuration found for this facility.';

  private _displayReportDashboard: boolean = false;

  @Input() set displayReportDashboard(v: boolean) {
    if (v !== null)
      this._displayReportDashboard = v;
  }
  get displayReportDashboard() { return this._displayReportDashboard; }

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

  showCensusDialog(): void {
    this.dialog.open(CensusConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Census Configuration', formMode: this.showNoCensusConfigAlert ? FormMode.Create : FormMode.Edit, viewOnly: false, censusConfig: this.censusConfig }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {        
          this.censusService.getConfiguration(this.facilityId).subscribe((data: ICensusConfiguration) => {
            if(data) {
              this.showNoCensusConfigAlert = false;
              this.censusConfig = data;
            }           
          }); 
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
        if(this.censusConfig) {
          this.showNoCensusConfigAlert = false;
        }
        else {
          this.showNoCensusConfigAlert = true;
        }
        
      }, error => {
        if(error.status == 404) {
          this.snackBar.open(`No current census configuration found for facility ${this.facilityId}, please create one.`, '', {
            duration: 3500,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.censusConfig = { facilityId: this.facilityConfig.facilityId, scheduledTrigger: ''} as ICensusConfiguration;
          this.showNoCensusConfigAlert = true;
          this.showCensusDialog();
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
