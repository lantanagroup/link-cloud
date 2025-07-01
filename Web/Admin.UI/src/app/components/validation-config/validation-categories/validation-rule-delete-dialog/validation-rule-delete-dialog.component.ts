import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
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
  ruleRegex: string = '';
  ruleField: string = '';
  
  constructor(private dialog: MatDialog) {
    // This will be set by the parent component after dialog opens
  }

  onConfirm(): void {
    // TODO: Implement confirm logic
    
    // Close all open dialogs on success
    this.dialog.closeAll();
  }
}
