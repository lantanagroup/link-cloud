import { CommonModule } from '@angular/common';
import { Component, Inject, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IDataAcquisitionFhirListConfigModel } from '../../../interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import { IEntityCreatedResponse } from '../../../interfaces/entity-created-response.model';
import { FormMode } from '../../../models/FormMode.enum';
import { DataAcquisitionFhirListConfigFormComponent } from '../data-acquisition-fhir-list-config-form/data-acquisition-fhir-list-config-form.component';

@Component({
  selector: 'app-data-acquisition-fhir-list-config-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    DataAcquisitionFhirListConfigFormComponent
  ],
  templateUrl: './data-acquisition-fhir-list-config-dialog.component.html',
  styleUrls: ['./data-acquisition-fhir-list-config-dialog.component.css']
})
export class DataAcquisitionFhirListConfigDialogComponent {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: IDataAcquisitionFhirListConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(DataAcquisitionFhirListConfigFormComponent) configForm!: DataAcquisitionFhirListConfigFormComponent;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, config: IDataAcquisitionFhirListConfigModel },
    private dialogRef: MatDialogRef<DataAcquisitionFhirListConfigFormComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.config;
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
      this.snackBar.open(`Failed to create data acquisition fhir list configuration for the facility, see error for details.`, '', {
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
