import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { ReportConfigFormComponent } from '../report-config-form/report-config-form.component';
import { IReportConfigModel } from '../../../interfaces/report/report-config-model.interface';

@Component({
    selector: 'app-report-config-dialog',
    standalone: true,
  templateUrl: './report-config-dialog.component.html',
  styleUrls: ['./report-config-dialog.component.scss'],
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        ReportConfigFormComponent
    ]
})
export class ReportConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  reportConfig!: IReportConfigModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(ReportConfigFormComponent) reportConfigForm!: ReportConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, reportConfig: IReportConfigModel },
    private dialogRef: MatDialogRef<ReportConfigDialogComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.reportConfig = this.data.reportConfig;
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
      this.dialogRef.close(outcome);
    }
    else {
      this.snackBar.open(`Failed to create report configuration for the facility, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.reportConfigForm.submitConfiguration();
  }

  canSave() {
    return (this.reportConfigForm?.configForm.status == 'VALID') ? true : false;
  }

}

