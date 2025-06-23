import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { MatDialogModule } from '@angular/material/dialog';
import { VdButtonComponent } from 'src/app/components/core/vd-button/vd-button.component';

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
  
}
