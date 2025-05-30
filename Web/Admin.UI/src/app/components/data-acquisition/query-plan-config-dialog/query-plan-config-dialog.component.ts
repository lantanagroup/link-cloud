import {Component, Inject, ViewChild} from '@angular/core';
import {CommonModule} from "@angular/common";
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from "@angular/material/dialog";
import {MatButtonModule} from "@angular/material/button";
import {MatIconModule} from "@angular/material/icon";
import {QueryPlanConfigFormComponent} from "../query-plan-config/query-plan-config.component";
import {IQueryPlanModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";
import {MatSnackBar} from "@angular/material/snack-bar";
import {FormMode} from "../../../models/FormMode.enum";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";

@Component({
  selector: 'app-query-plan-config-dialog',
  standalone: true,
  imports: [ CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    QueryPlanConfigFormComponent],
  templateUrl: './query-plan-config-dialog.component.html',
  styleUrl: './query-plan-config-dialog.component.scss'
})
export class QueryPlanConfigDialogComponent {

  @ViewChild(QueryPlanConfigFormComponent) configForm!: QueryPlanConfigFormComponent;

  dialogTitle: string = '';
  viewOnly: boolean = false;
  config!: IQueryPlanModel;
  formMode!: FormMode;
  formIsInvalid: boolean = true;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, formMode: FormMode, viewOnly: boolean, dataAcqQueryPlanConfig: IQueryPlanModel },
              private dialogRef: MatDialogRef<QueryPlanConfigFormComponent>,
              private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.config = this.data.dataAcqQueryPlanConfig;
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
      this.snackBar.open(`Failed to create data acquisition query plan configuration for the facility, see error for details.`, '', {
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
