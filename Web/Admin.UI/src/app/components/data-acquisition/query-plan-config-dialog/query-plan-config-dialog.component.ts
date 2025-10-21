import {Component, ElementRef, Inject, ViewChild} from '@angular/core';

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
  imports: [MatDialogModule, MatButtonModule, MatIconModule, QueryPlanConfigFormComponent],
  templateUrl: './query-plan-config-dialog.component.html',
  styleUrl: './query-plan-config-dialog.component.scss'
})
export class QueryPlanConfigDialogComponent {

  @ViewChild(QueryPlanConfigFormComponent) configForm!: QueryPlanConfigFormComponent;
  @ViewChild('jsonFileInput') jsonFileInput!: ElementRef<HTMLInputElement>;

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

  triggerJsonImport() {
    if (this.viewOnly) {
      return;
    }
    const input = this.jsonFileInput?.nativeElement;
    if (input) {
      input.value = '';
      input.click();
    }
  }

  async onJsonFileSelected(event: Event) {
    if (this.viewOnly) {
      return;
    }

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    try {
      const rawText = await file.text();
      const parsedPlan = JSON.parse(rawText);
      this.applyImportedPlan(parsedPlan);
    } catch (error) {
      this.snackBar.open('Failed to import query plan JSON. Please verify the file format.', '', {
        duration: 5000,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    } finally {
      input.value = '';
    }
  }

  exportJson() {
    const json = this.configForm?.currentPlanJson;
    if (!json) {
      this.snackBar.open('No query plan data available to export.', '', {
        duration: 4000,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      return;
    }

    const blob = new Blob([json], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = this.getExportFileName();
    link.click();
    window.URL.revokeObjectURL(url);
  }

  private applyImportedPlan(plan: any) {
    if (!this.configForm) {
      return;
    }

    const currentFacilityId = this.configForm.facilityIdControl.value;
    const incomingFacilityId = plan?.FacilityId ?? plan?.facilityId ?? '';
    const preserveFacilityId = !!currentFacilityId;

    const currentType = this.configForm.typeControl.value;
    const incomingType = plan?.Type ?? plan?.type ?? '';
    const preserveType = this.configForm.typeControl.disabled && !!currentType;

    const error = this.configForm.applyImportedPlan(plan, preserveFacilityId, preserveType);
    if (error) {
      this.snackBar.open(error, '', {
        duration: 5000,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      return;
    }

    if (preserveFacilityId && incomingFacilityId && incomingFacilityId !== currentFacilityId) {
      this.snackBar.open('Imported FacilityId does not match the current facility. Keeping the current facility.', '', {
        duration: 5000,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }

    if (preserveType && incomingType && incomingType !== currentType) {
      this.snackBar.open('Imported Type does not match the current plan type. Keeping the current type.', '', {
        duration: 5000,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  private getExportFileName(): string {
    const facilityId = this.sanitizeFileSegment(this.configForm?.facilityIdControl.value ?? 'facility');
    const type = this.sanitizeFileSegment(this.configForm?.typeControl.value ?? 'plan');
    const planName = this.sanitizeFileSegment(this.configForm?.planNameControl.value ?? 'query-plan');

    return `query-plan-${facilityId}-${type}-${planName || 'config'}.json`;
  }

  private sanitizeFileSegment(value: string): string {
    return value
      .toString()
      .trim()
      .replace(/[^A-Za-z0-9._-]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  }

}
