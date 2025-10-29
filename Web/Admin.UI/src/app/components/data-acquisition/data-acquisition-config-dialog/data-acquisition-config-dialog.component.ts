import { Component, Inject, OnInit, ViewChild } from '@angular/core';

import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ITenantDataAcquisitionConfigModel } from 'src/app/interfaces/data-acquisition/data-acquisition-config-model.interface';
import { FormMode } from 'src/app/models/FormMode.enum';
import { DataAcquisitionConfigFormComponent } from '../data-acquisition-config-form/data-acquisition-config-form.component';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';

@Component({
  selector: 'app-data-acquisition-config-dialog',
  standalone: true,
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    DataAcquisitionConfigFormComponent
],
  templateUrl: './data-acquisition-config-dialog.component.html',
  styleUrls: ['./data-acquisition-config-dialog.component.scss']
})
export class DataAcquisitionConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: ITenantDataAcquisitionConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(DataAcquisitionConfigFormComponent) configForm!: DataAcquisitionConfigFormComponent;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, config: ITenantDataAcquisitionConfigModel },
  private dialogRef: MatDialogRef<DataAcquisitionConfigFormComponent>,
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
