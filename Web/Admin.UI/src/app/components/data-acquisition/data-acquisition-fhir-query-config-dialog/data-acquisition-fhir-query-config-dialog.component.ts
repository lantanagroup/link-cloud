import { CommonModule } from '@angular/common';
import { Component, Inject, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IDataAcquisitionQueryConfigModel } from '../../../interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { FormMode } from '../../../models/FormMode.enum';
import { DataAcquisitionFhirQueryConfigFormComponent } from '../data-acquisition-fhir-query-config-form/data-acquisition-fhir-query-config-form.component';


@Component({
  selector: 'app-data-acquisition-fhir-query-config-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    DataAcquisitionFhirQueryConfigFormComponent
  ],
  templateUrl: './data-acquisition-fhir-query-config-dialog.component.html',
  styleUrls: ['./data-acquisition-fhir-query-config-dialog.component.scss']
})
export class DataAcquisitionFhirQueryConfigDialogComponent {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: IDataAcquisitionQueryConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(DataAcquisitionFhirQueryConfigFormComponent) configForm!: DataAcquisitionFhirQueryConfigFormComponent;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, dataAcqFhirQueryConfig: IDataAcquisitionQueryConfigModel },
    private dialogRef: MatDialogRef<DataAcquisitionFhirQueryConfigFormComponent>,
    private snackBar: MatSnackBar) {

  }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.dataAcqFhirQueryConfig;
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
    if (outcome.message.length > 0) {
      this.dialogRef.close(outcome.message);
    }
    else {
      this.snackBar.open(`Failed to create data acquisition configuration for the facility, see error for details.`, '', {
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
