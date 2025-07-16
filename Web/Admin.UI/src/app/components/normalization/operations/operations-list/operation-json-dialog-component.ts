import { Component, Inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle
} from '@angular/material/dialog';
import {JsonPipe} from "@angular/common";
import {MatButton} from "@angular/material/button";

@Component({
  selector: 'app-operation-json-dialog',
  template: `
    <h2 mat-dialog-title>Operation JSON</h2>
    <mat-dialog-content>
      <pre>{{ data | json }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  imports: [
    MatDialogActions,
    MatDialogContent,
    JsonPipe,
    MatButton,
    MatDialogClose,
    MatDialogTitle
  ],
  styles: [`
    pre {
      white-space: pre-wrap;
      word-break: break-word;
      background: #f5f5f5;
      padding: 1rem;
      border-radius: 4px;
      font-family: monospace;
    }
  `]
})
export class OperationJsonDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: any) {}
}
