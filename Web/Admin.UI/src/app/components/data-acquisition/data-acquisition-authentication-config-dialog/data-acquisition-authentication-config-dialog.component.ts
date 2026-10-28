
import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IDataAcquisitionAuthenticationConfigModel } from '../../../interfaces/data-acquisition/data-acquisition-auth-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { FormMode } from '../../../models/FormMode.enum';
import { DataAcquisitionAuthenticationConfigFormComponent } from '../data-acquisition-authentication-config-form/data-acquisition-authentication-config-form.component';

@Component({
  selector: 'app-data-acquisition-authentication-config-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    DataAcquisitionAuthenticationConfigFormComponent
],
  templateUrl: './data-acquisition-authentication-config-dialog.component.html',
  styleUrls: ['./data-acquisition-authentication-config-dialog.component.css']
})
export class DataAcquisitionAuthenticationConfigDialogComponent implements OnInit{
  dialogTitle: string = '';
  viewOnly: boolean = false;
  queryAuthConfig!: IDataAcquisitionAuthenticationConfigModel;
  queryListAuthConfig!: IDataAcquisitionAuthenticationConfigModel;
  facilityId!: string;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(DataAcquisitionAuthenticationConfigFormComponent) configForm!: DataAcquisitionAuthenticationConfigFormComponent;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, queryAuthConfig: IDataAcquisitionAuthenticationConfigModel, queryListAuthConfig: IDataAcquisitionAuthenticationConfigModel, facilityId: string },
    private dialogRef: MatDialogRef<DataAcquisitionAuthenticationConfigFormComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.queryAuthConfig = this.data.queryAuthConfig;
    this.queryListAuthConfig = this.data.queryListAuthConfig
    this.facilityId = this.data.facilityId;
    this.formMode = this.data.formMode;
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IEntityCreatedResponse) {
    if (outcome.id.length > 0) {
      this.dialogRef.close(outcome.message);
    }
    else {
      this.snackBar.open(`Failed to create data acquisition authentication configuration for the facility, see error for details.`, '', {
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
