import { Component, Inject } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';


import { IValidationRule } from 'src/app/components/tenant/facility-view/report-view.interface';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

export interface RuleDialogData {
  dialogTitle: string;
  rule: IValidationRule;
}

@Component({
  selector: 'app-rule-add-edit-dialog',
  imports: [
    MatDialogModule,
    MatTableModule,
    MatSortModule,
    VdButtonComponent,
    FormsModule,
    ReactiveFormsModule
],
  templateUrl: './validation-rule-add-edit-dialog.component.html',
  styleUrls: ['./validation-rule-add-edit-dialog.component.scss'],
  standalone: true,
})
export class RuleAddEditDialogComponent {
  dialogTitle: string;
  rule: IValidationRule;

  constructor(@Inject(MAT_DIALOG_DATA) public data: RuleDialogData) {
    this.dialogTitle = data.dialogTitle;
    this.rule = data.rule;
  }
}
