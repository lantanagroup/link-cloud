import { Component, Inject, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IOrganizationLocationConfigurationModel } from '../../../interfaces/data-acquisition/organization-location-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { Vendor } from '../../../interfaces/tenant/vendor.enum';
import { FormMode } from '../../../models/FormMode.enum';
import { ReportingOrganizationConfigFormComponent } from '../reporting-organization-config-form/reporting-organization-config-form.component';

@Component({
  selector: 'app-reporting-organization-config-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    ReportingOrganizationConfigFormComponent
  ],
  templateUrl: './reporting-organization-config-dialog.component.html',
  styleUrls: ['./reporting-organization-config-dialog.component.scss']
})
export class ReportingOrganizationConfigDialogComponent {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: IOrganizationLocationConfigurationModel;
  formMode!: FormMode;
  vendor!: Vendor;
  formIsInvalid: boolean = true;

  @ViewChild(ReportingOrganizationConfigFormComponent) configForm!: ReportingOrganizationConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      formMode: FormMode,
      viewOnly: boolean,
      vendor: Vendor,
      reportingOrgConfig: IOrganizationLocationConfigurationModel
    },
    private dialogRef: MatDialogRef<ReportingOrganizationConfigDialogComponent>,
    private snackBar: MatSnackBar
  ) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.reportingOrgConfig;
    this.formMode = this.data.formMode;
    this.vendor = this.data.vendor;
  }

  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IEntityCreatedResponse) {
    if (outcome.message.length > 0) {
      this.dialogRef.close(outcome.message);
    } else {
      this.snackBar.open('Failed to save reporting organization configuration, see error for details.', '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.configForm.submitConfiguration();
  }
}
