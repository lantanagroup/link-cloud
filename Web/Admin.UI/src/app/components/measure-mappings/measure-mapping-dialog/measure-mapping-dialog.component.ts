import { Component, Inject, OnInit, ViewChild } from '@angular/core';

import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MeasureMappingFormComponent } from '../measure-mapping-form/measure-mapping-form.component';
import { IApiResponse } from '../../../interfaces/api-response.interface';
import { IMeasureMapping } from '../../../interfaces/dmrp/measure-mapping.interface';

@Component({
  selector: 'app-measure-mapping-dialog',
  standalone: true,
  templateUrl: './measure-mapping-dialog.component.html',
  styleUrls: ['./measure-mapping-dialog.component.scss'],
  imports: [
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MeasureMappingFormComponent
  ]
})
export class MeasureMappingDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  measureMapping!: IMeasureMapping;
  formMode!: FormMode;
  canSave = false;

  @ViewChild(MeasureMappingFormComponent) measureMappingForm!: MeasureMappingFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      formMode: FormMode,
      viewOnly: boolean,
      measureMapping: IMeasureMapping,
    },
    private dialogRef: MatDialogRef<MeasureMappingDialogComponent>,
    private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.measureMapping = this.data.measureMapping;
    this.formMode = this.data.formMode;
  }

  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formInvalid: boolean) {
    this.canSave = !formInvalid;
  }

  onSubmittedConfiguration(outcome: IApiResponse) {
    if (outcome.success) {
      this.dialogRef.close(outcome);
    } else {
      this.snackBar.open(outcome.message, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    if (!this.measureMappingForm) {
      this.snackBar.open('Form not initialized', '', {
        duration: 3500,
        panelClass: 'error-snackbar'
      });
      return;
    }
    this.measureMappingForm.submitConfiguration();
  }
}
