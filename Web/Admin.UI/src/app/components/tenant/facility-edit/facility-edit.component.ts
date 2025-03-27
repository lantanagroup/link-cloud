import {Component, OnInit, Input, ViewChild} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ActivatedRoute, RouterLink} from '@angular/router';
import {TenantService} from 'src/app/services/gateway/tenant/tenant.service';
import {IFacilityConfigModel} from 'src/app/interfaces/tenant/facility-config-model.interface';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatCardModule} from '@angular/material/card';
import {FacilityConfigFormComponent} from '../facility-config-form/facility-config-form.component';
import {MatSnackBar} from '@angular/material/snack-bar';
import {FacilityConfigDialogComponent} from '../facility-config-dialog/facility-config-dialog.component';
import {MatDialog, MatDialogModule} from '@angular/material/dialog';
import {MatAccordion, MatExpansionModule} from '@angular/material/expansion';
import {MatTabsModule} from '@angular/material/tabs';
import {CensusConfigDialogComponent} from '../../census/census-config-dialog/census-config-dialog.component';
import {CensusService} from 'src/app/services/gateway/census/census.service';
import {DataAcquisitionService} from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {ICensusConfiguration} from 'src/app/interfaces/census/census-config-model.interface';
import {CensusConfigFormComponent} from "../../census/census-config-form/census-config-form.component";
import {LinkAlertComponent} from "../../core/link-alert/link-alert.component";
import {LinkAlertType} from '../../core/link-alert/link-alert-type.enum';
import {FormMode} from 'src/app/models/FormMode.enum';
import {DataAcquisitionConfigFormComponent} from '../../data-acquisition/data-acquisition-config-form/data-acquisition-config-form.component';
import {IDataAcquisitionQueryConfigModel} from '../../../interfaces/data-acquisition/data-acquisition-config-model.interface';
import {IDataAcquisitionFhirListConfigModel} from '../../../interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import {DataAcquisitionFhirQueryConfigDialogComponent} from '../../data-acquisition/data-acquisition-fhir-query-config-dialog/data-acquisition-fhir-query-config-dialog.component';
import {DataAcquisitionFhirQueryConfigFormComponent} from '../../data-acquisition/data-acquisition-fhir-query-config-form/data-acquisition-fhir-query-config-form.component';
import {DataAcquisitionFhirListConfigDialogComponent} from '../../data-acquisition/data-acquisition-fhir-list-config-dialog/data-acquisition-fhir-list-config-dialog.component';
import {DataAcquisitionFhirListConfigFormComponent} from '../../data-acquisition/data-acquisition-fhir-list-config-form/data-acquisition-fhir-list-config-form.component';
import {IQueryPlanModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";
import {QueryPlanConfigDialogComponent} from "../../data-acquisition/query-plan-config-dialog/query-plan-config-dialog.component";
import {QueryPlanConfigFormComponent} from "../../data-acquisition/query-plan-config/query-plan-config.component";

@Component({
  selector: 'app-facility-edit',
  standalone: true,
  templateUrl: './facility-edit.component.html',
  styleUrls: ['./facility-edit.component.scss'],
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
    DataAcquisitionConfigFormComponent,
    DataAcquisitionFhirQueryConfigFormComponent,
    DataAcquisitionFhirListConfigFormComponent,
    DataAcquisitionFhirListConfigDialogComponent,
    QueryPlanConfigFormComponent
  ]
})
export class FacilityEditComponent implements OnInit {
  @ViewChild(MatAccordion) accordion!: MatAccordion;

  @ViewChild(QueryPlanConfigFormComponent) configForm!: QueryPlanConfigFormComponent;

  facilityId: string = '';
  facilityConfig!: IFacilityConfigModel;
  facilityConfigFormViewOnly: boolean = true;
  facilityConfigFormIsInvalid: boolean = false;

  censusConfig!: ICensusConfiguration;
  dataAcqFhirQueryConfig!: IDataAcquisitionQueryConfigModel;
  dataAcqFhirListConfig!: IDataAcquisitionFhirListConfigModel;

  linkNoConfigAlertType = LinkAlertType.info;
  showNoCensusConfigAlert: boolean = false;
  noCensusConfigAlertMessage = 'No census configuration found for this facility.';

  noDataAcqFhirQueryConfigAlertMessage = 'No FHIR query configuration found for this facility.';
  showNoDataAcqFhirQueryConfigAlert: boolean = false;
  noDataAcqFhirListConfigAlertMessage = 'No FHIR List configuration found for this facility.';
  showNoDataAcqFhirListConfigAlert: boolean = false;

  noDataAcqQueryPlanConfigAlertMessage = 'No FHIR query plan found for this facility.';
  showNoDataAcqQueryPlanConfigAlert: boolean = false;

  dataAcqQueryPlanConfig!: IQueryPlanModel;

  dataAcqQueryPlanNames: string[] = [];


  private _displayReportDashboard: boolean = false;

  @Input() set displayReportDashboard(v: boolean) {
    if (v !== null)
      this._displayReportDashboard = v;
  }

  get displayReportDashboard() {
    return this._displayReportDashboard;
  }

  constructor(
    private route: ActivatedRoute,
    private tenantService: TenantService,
    private censusService: CensusService,
    private dataAcquisitionService: DataAcquisitionService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar) {
  }

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
        data: {dialogTitle: 'Edit facility', viewOnly: false, facilityConfig: this.facilityConfig}
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
        data: {
          dialogTitle: 'Census Configuration',
          formMode: this.showNoCensusConfigAlert ? FormMode.Create : FormMode.Edit,
          viewOnly: false,
          censusConfig: this.censusConfig
        }
      }).afterClosed().subscribe(res => {
      console.log(res)
      if (res) {
        this.censusService.getConfiguration(this.facilityId).subscribe((data: ICensusConfiguration) => {
          if (data) {
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
    if (!this.censusConfig) {
      this.censusService.getConfiguration(this.facilityId).subscribe((data: ICensusConfiguration) => {
        this.censusConfig = data;
        if (this.censusConfig) {
          this.showNoCensusConfigAlert = false;
        } else {
          this.showNoCensusConfigAlert = true;
        }

      }, error => {
        if (error.status == 404) {
          this.snackBar.open(`No current census configuration found for facility ${this.facilityId}, please create one.`, '', {
            duration: 3500,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.censusConfig = {
            facilityId: this.facilityConfig.facilityId,
            scheduledTrigger: ''
          } as ICensusConfiguration;
          this.showNoCensusConfigAlert = true;
          this.showCensusDialog();
        } else {
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

  showDataAcqFhirQueryDialog(): void {
    this.dialog.open(DataAcquisitionFhirQueryConfigDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Fhir Query Configuration',
          formMode: this.showNoDataAcqFhirQueryConfigAlert ? FormMode.Create : FormMode.Edit,
          viewOnly: false,
          dataAcqFhirQueryConfig: this.dataAcqFhirQueryConfig
        }
      }).afterClosed().subscribe(res => {
      console.log(res)
      if (res) {
        this.dataAcquisitionService.getFhirQueryConfiguration(this.facilityId).subscribe((data: IDataAcquisitionQueryConfigModel) => {
          if (data) {
            this.showNoDataAcqFhirQueryConfigAlert = false;
            this.dataAcqFhirQueryConfig = data;
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

  showDataAcqFhirListDialog(): void {
    this.dialog.open(DataAcquisitionFhirListConfigDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Fhir Query List Configuration',
          formMode: this.showNoDataAcqFhirListConfigAlert ? FormMode.Create : FormMode.Edit,
          viewOnly: false,
          dataAcqFhirListConfig: this.dataAcqFhirListConfig
        }
      }).afterClosed().subscribe(res => {
      console.log(res)
      if (res) {
        this.dataAcquisitionService.getFhirListConfiguration(this.facilityId).subscribe((data: IDataAcquisitionFhirListConfigModel) => {
          if (data) {
            console.log(data);
            this.showNoDataAcqFhirListConfigAlert = false;
            this.dataAcqFhirListConfig = data;
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

  showDataAcqQueryPlanDialog(): void {
    this.dialog.open(QueryPlanConfigDialogComponent,
      {
        width: '75%',
        data: {
          dialogTitle: 'Fhir Query Plan Configuration',
          formMode: this.showNoDataAcqQueryPlanConfigAlert ? FormMode.Create : FormMode.Edit,
          viewOnly: false,
          dataAcqQueryPlanConfig: this.dataAcqQueryPlanConfig
        }
      }).afterClosed().subscribe(res => {
      console.log(res)
      if (res) {
        this.dataAcquisitionService.getQueryPlanConfiguration(this.facilityId, this.dataAcqQueryPlanConfig.Type).subscribe((data: IQueryPlanModel) => {
          if (data) {
            console.log(data);
            this.showNoDataAcqQueryPlanConfigAlert = false;
            this.dataAcqQueryPlanConfig = data;
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


  loadDataAcquisitionConfig() {
    this.loadFhirQueryConfig();
    this.loadFhirListConfig();
    this.loadQueryPlan("0", "Discharge");
  }

  loadFhirQueryConfig() {
    if (!this.dataAcqFhirQueryConfig) {
      this.dataAcquisitionService.getFhirQueryConfiguration(this.facilityId).subscribe((data: IDataAcquisitionQueryConfigModel) => {
        this.dataAcqFhirQueryConfig = data;
        if (this.dataAcqFhirQueryConfig) {
          this.showNoDataAcqFhirQueryConfigAlert = false;
        } else {
          this.showNoDataAcqFhirQueryConfigAlert = true;
        }
      }, error => {
        if (error.status == 404) {
          this.snackBar.open(`No current FHIR query configuration found for facility ${this.facilityId}, please create one.`, '', {
            duration: 3500,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.dataAcqFhirQueryConfig = {
            id: '',
            facilityId: this.facilityConfig.facilityId,
            fhirServerBaseUrl: '',
            queryPlanIds: []
          } as IDataAcquisitionQueryConfigModel;
          this.showNoDataAcqFhirQueryConfigAlert = true;
          //this.showDataAcqFhirQueryDialog();
        } else {
          this.snackBar.open(`Failed to load FHIR query configuration for the facility, see error for details.`, '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
    }
  }

  loadFhirListConfig() {
    if (!this.dataAcqFhirListConfig) {
      this.dataAcquisitionService.getFhirListConfiguration(this.facilityId).subscribe((data: IDataAcquisitionFhirListConfigModel) => {
        this.dataAcqFhirListConfig = data;
        if (this.dataAcqFhirListConfig) {
          this.showNoDataAcqFhirListConfigAlert = false;
        } else {
          this.showNoDataAcqFhirListConfigAlert = true;
        }
      }, error => {
        if (error.status == 404) {
          this.snackBar.open(`No current FHIR list configuration found for facility ${this.facilityId}, please create one.`, '', {
            duration: 3500,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.dataAcqFhirListConfig = {
            id: '',
            facilityId: this.facilityConfig.facilityId,
            fhirBaseServerUrl: '',
            ehrPatientLists: []
          } as IDataAcquisitionFhirListConfigModel;
          this.showNoDataAcqFhirListConfigAlert = true;
          //this.showDataAcqFhirQueryDialog();
        } else {
          this.snackBar.open(`Failed to load FHIR list configuration for the facility, see error for details.`, '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
    }
  }

  onPlanSelected(outcome: any) {
    this.dataAcqQueryPlanConfig.Type = outcome.type;
    this.loadQueryPlan(outcome.type, outcome.label);
    if (outcome.exists) {
      this.showNoDataAcqQueryPlanConfigAlert = false;
    } else {
      this.showNoDataAcqQueryPlanConfigAlert = true;
    }
  }

  loadQueryPlan(type: string, label:string) {
    this.dataAcquisitionService.getQueryPlanConfiguration(this.facilityId, label).subscribe((data: IQueryPlanModel) => {
      this.dataAcqQueryPlanConfig = data;
      if (this.dataAcqQueryPlanConfig) {
        this.showNoDataAcqQueryPlanConfigAlert = false;
      } else {
        this.showNoDataAcqQueryPlanConfigAlert = true;
      }
    }, error => {
      if (error.status == 404) {
        this.snackBar.open(`No current FHIR query plan found for facility ${this.facilityId} and type ${label} , please create one.`, '', {
          duration: 3500,
          panelClass: 'info-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.dataAcqQueryPlanConfig = {
          FacilityId: this.facilityConfig.facilityId,
          PlanName: '',
          EHRDescription: '',
          LookBack: '',
          InitialQueries: '',
          SupplementalQueries: '',
          Type: type
        } as IQueryPlanModel;
        this.showNoDataAcqQueryPlanConfigAlert = true;
        //this.showDataAcqFhirQueryDialog();
      } else {
        this.snackBar.open(`Failed to load FHIR query plan for the facility ${this.facilityId} and type ${type}, see error for details.`, '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

}
