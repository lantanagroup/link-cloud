import {Component, Inject, ViewChild} from '@angular/core';
import {QueryPlanConfigFormComponent} from "../../data-acquisition/query-plan-config/query-plan-config.component";
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";
import {NormalizationFormComponent} from "../normalization-config/normalization.component";

import {MatButtonModule} from "@angular/material/button";
import {MatIconModule} from "@angular/material/icon";
import { FormMode } from '../../../models/FormMode.enum';
import {INormalizationModel} from "../../../interfaces/normalization/normalization-model.interface";

@Component({
  selector: 'app-normalization-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, NormalizationFormComponent],
  templateUrl: './normalization-dialog.component.html',
  styleUrl: './normalization-dialog.component.scss'
})
export class NormalizationConfigDialogComponent {

  @ViewChild(NormalizationFormComponent) configForm!: NormalizationFormComponent;

  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: INormalizationModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, normalization: INormalizationModel },
              private dialogRef: MatDialogRef<NormalizationFormComponent>,
              private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.normalization;
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
    if (outcome.id.length > 0 || outcome.message.length > 0) {
      this.dialogRef.close(outcome.message);
    }
    else {
      this.snackBar.open(`Failed to create normalization configuration for the facility, see error for details.`, '', {
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
