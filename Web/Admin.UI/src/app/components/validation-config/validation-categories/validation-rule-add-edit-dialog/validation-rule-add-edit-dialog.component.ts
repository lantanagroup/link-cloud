import { Component, Inject } from '@angular/core';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';

import { CommonModule } from '@angular/common';
import { IValidationRule } from 'src/app/components/tenant/facility-view/report-view.interface';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { VdIconComponent } from "../../../core/vd-icon/vd-icon.component";

export interface RuleDialogData {
  dialogTitle: string;
  rule: IValidationRule;
}

@Component({
  selector: 'app-rule-add-edit-dialog',
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatSortModule,
    VdButtonComponent,
    FormsModule,
    ReactiveFormsModule,
    VdIconComponent
],
  templateUrl: './validation-rule-add-edit-dialog.component.html',
  styleUrls: ['./validation-rule-add-edit-dialog.component.scss'],
  standalone: true,
})
export class RuleAddEditDialogComponent {
  dialogTitle: string;
  rule: IValidationRule;

  constructor(
    public dialogRef: MatDialogRef<RuleAddEditDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: RuleDialogData
  ) {
    this.dialogTitle = data.dialogTitle;
    this.rule = data.rule;
  }

  onSave(): void {
    // Just closing the dialog for now, save functionality to be added later
    this.dialogRef.close();
  }
}
