import { Component, Inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { FormMode } from 'src/app/models/FormMode.enum';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import {IQueryDispatchConfiguration} from "../../../interfaces/query-dispatch/query-dispatch-config-model.interface";
import {QueryDispatchConfigFormComponent} from "../query-dispatch-config-form/query-dispatch-config-form.component";

@Component({
    selector: 'app-query-dispatch-config-dialog',
    standalone: true,
    templateUrl: './query-dispatch-config-dialog.component.html',
    styleUrls: ['./query-dispatch-config-dialog.component.scss'],
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    QueryDispatchConfigFormComponent
  ]
})
export class QueryDispatchConfigDialogComponent implements OnInit {
  dialogTitle: string = '';
  viewOnly: boolean = false;
  queryDispatchConfig!: IQueryDispatchConfiguration;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  @ViewChild(QueryDispatchConfigFormComponent) queryConfigForm!: QueryDispatchConfigFormComponent;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, queryDispatchConfig: IQueryDispatchConfiguration },
    private dialogRef: MatDialogRef<QueryDispatchConfigDialogComponent>,
    private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.queryDispatchConfig = this.data.queryDispatchConfig;
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
    if (outcome.message.length > 0)  {
      this.dialogRef.close(outcome.message);
    }
    else {
      this.snackBar.open(`Failed to create query dispatch configuration for the facility, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    this.queryConfigForm.submitConfiguration();
  }

}
