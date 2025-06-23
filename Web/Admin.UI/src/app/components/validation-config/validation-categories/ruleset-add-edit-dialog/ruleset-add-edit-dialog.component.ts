import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ValidationRuleDeleteDialogComponent } from '../validation-rule-delete-dialog/validation-rule-delete-dialog.component';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';
import { VdIconComponent } from 'src/app/components/core/vd-icon/vd-icon.component';

export interface RulesetDialogData {
  dialogTitle: string;
  rules: any[];
}

@Component({
  selector: 'app-ruleset-add-edit-dialog',
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatSortModule,
    VdButtonComponent,
    VdIconComponent
  ],
  templateUrl: './ruleset-add-edit-dialog.component.html',
  styleUrls: ['./ruleset-add-edit-dialog.component.scss'],
  standalone: true,
})
export class RulesetAddEditDialogComponent {
  dialogTitle: string = 'Add Rule Set';
  rules: any[] = [
    {
      field: "Details",
      regex: "^Wrong Display Name '.*' for .* should be .*'.*'.*"
    },
    {
      field: "Severity",
      regex: "^ERROR$"
    }
  ];

  columns: string[] = ['field', 'regex', 'actions'];

  constructor(private dialog: MatDialog) {}

  onDelete(rule: any): void {
    const ruleData = {
      ruleRegex: rule.regex || rule.matcher?.regex || '',
      ruleField: rule.field || rule.matcher?.field || ''
    };
    
    const dialogRef = this.dialog.open(ValidationRuleDeleteDialogComponent, {
      width: '380px',
      panelClass: ['vd-dialog', 'confirm-delete-dialog']
    });
    
    // Set the properties directly on the component instance
    dialogRef.componentInstance.ruleRegex = ruleData.ruleRegex;
    dialogRef.componentInstance.ruleField = ruleData.ruleField;
  }
}
