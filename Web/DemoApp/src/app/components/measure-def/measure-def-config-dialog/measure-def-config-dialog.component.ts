import { AfterViewInit, Component, OnInit, Inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { IMeasureDefinitionConfigModel } from '../../../interfaces/measure-definition/measure-definition-config-model.interface';
import { MeasureDefinitionFormComponent } from '../measure-def-config-form/measure-def-config-form.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-measure-def-config-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MeasureDefinitionFormComponent,
    MatProgressSpinnerModule
  ],
  templateUrl: './measure-def-config-dialog.component.html',
  styleUrls: ['./measure-def-config-dialog.component.scss']
})
export class MeasureDefinitionDialogComponent implements OnInit, AfterViewInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  measureDefConfig!: IMeasureDefinitionConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(MeasureDefinitionFormComponent) measureDefinitionForm!: MeasureDefinitionFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, viewOnly: boolean, formMode: FormMode, measureDefConfig: IMeasureDefinitionConfigModel },
    private dialogRef: MatDialogRef<MeasureDefinitionDialogComponent>,
    private snackBar: MatSnackBar) { dialogRef.disableClose = true; }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.measureDefConfig = this.data.measureDefConfig;
    this.formMode = this.data.formMode;
  }

  ngAfterViewInit() {
    console.log('Values on ngAfterViewInit():');
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
      this.snackBar.open(`Failed to create/update measure definition, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  get processing() {
    return this.measureDefinitionForm?.processing;
  }

  submitConfiguration() {
    this.measureDefinitionForm.submitConfiguration();
  }

}
