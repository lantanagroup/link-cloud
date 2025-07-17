import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { IValidationRule } from 'src/app/components/tenant/facility-view/report-view.interface';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';

export interface ValidationRuleDeleteDialogData {
  ruleRegex: string;
  ruleField: string;
}

@Component({
  selector: 'app-validation-rule-delete-dialog',
  imports: [
    CommonModule,
    MatDialogModule,
    VdButtonComponent,
  ],
  templateUrl: './validation-rule-delete-dialog.component.html',
  styleUrls: ['./validation-rule-delete-dialog.component.scss'],
  standalone: true,
})
export class ValidationRuleDeleteDialogComponent {
  rule: IValidationRule = {
    id: 0,
    matcher: {
      inverted: false,
      field: '',
      regex: ''
    },
    timestamp: ''
  }
  
  constructor(@Inject(MAT_DIALOG_DATA) public data: { rule: IValidationRule }, private dialog: MatDialog) {
    this.rule = data.rule;
  }

  onConfirm(): void {
    // Implement confirm logic here
    
    // Close all open dialogs on success
    this.dialog.closeAll();
  }
}
