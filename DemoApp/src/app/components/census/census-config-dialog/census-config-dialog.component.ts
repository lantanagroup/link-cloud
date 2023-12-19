import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { ICensusConfiguration } from 'src/app/interfaces/census/census-config-model.interface';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { CensusConfigFormComponent } from '../census-config-form/census-config-form.component';

@Component({
    selector: 'app-census-config-dialog',
    standalone: true,
    templateUrl: './census-config-dialog.component.html',
    styleUrls: ['./census-config-dialog.component.scss'],
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        CensusConfigFormComponent
    ]
})
export class CensusConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  censusConfig!: ICensusConfiguration;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(CensusConfigFormComponent) censusConfigForm!: CensusConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, censusConfig: ICensusConfiguration },
    private dialogRef: MatDialogRef<CensusConfigDialogComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.censusConfig = this.data.censusConfig;
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
      this.snackBar.open(`Failed to create census configuration for the facility, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }

  }

  submitConfiguration() {
    this.censusConfigForm.submitConfiguration();
  }


}
