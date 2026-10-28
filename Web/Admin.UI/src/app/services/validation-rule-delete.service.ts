import { Injectable } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ValidationRuleDeleteDialogComponent } from '../components/validation-config/validation-categories/validation-rule-delete-dialog/validation-rule-delete-dialog.component';

/*
This service is used to open the validation rule delete dialog.
It is used in the edit-validation-category component to open the dialog when the user clicks the delete button.
It is also used in the rule-add-edit-dialog component to open the dialog when the user clicks the delete button.
*/

export interface ValidationRuleDeleteData {
  ruleRegex: string;
  ruleField: string;
}

@Injectable({
  providedIn: 'root'
})
export class ValidationRuleDeleteService {

  constructor(private dialog: MatDialog) {}

  openDeleteDialog(ruleData: ValidationRuleDeleteData): void {
    const dialogRef = this.dialog.open(ValidationRuleDeleteDialogComponent, {
      width: '380px',
      panelClass: ['vd-dialog', 'confirm-delete-dialog']
    });
    
    // Set the properties directly on the component instance
    dialogRef.componentInstance.ruleRegex = ruleData.ruleRegex;
    dialogRef.componentInstance.ruleField = ruleData.ruleField;
  }

  openDeleteDialogFromRule(rule: any): void {
    const ruleData: ValidationRuleDeleteData = {
      ruleRegex: rule.regex || rule.matcher?.regex || '',
      ruleField: rule.field || rule.matcher?.field || ''
    };
    
    this.openDeleteDialog(ruleData);
  }
} 