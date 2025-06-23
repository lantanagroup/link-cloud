import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ValidationRuleDeleteDialogComponent } from '../validation-rule-delete-dialog/validation-rule-delete-dialog.component';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';
import { VdIconComponent } from 'src/app/components/core/vd-icon/vd-icon.component';

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
  columns: string[] = ['field', 'regex', 'actions'];
  rules = [
    {
      field: "Details",
      regex: "^Wrong Display Name '.*' for .* should be .*'.*'.*"
    },
    {
      field: "Severity",
      regex: "^ERROR$"
    }
  ]

  // onDelete(): void {
  //   this.dialog.open(ValidationRuleDeleteDialogComponent, {
  //     width: '830px',
  //     panelClass: 'vd-dialog',
  //     data: {
  //       dialogTitle: 'Edit Rule Set',
  //       // formMode: FormMode.Edit,
  //       // viewOnly: false,
  //       // rulesetConfig: {...row}
  //     }
  //   });
  // }
}
