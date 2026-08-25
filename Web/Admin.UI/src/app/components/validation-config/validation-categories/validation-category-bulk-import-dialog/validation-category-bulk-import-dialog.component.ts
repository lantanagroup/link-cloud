import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

@Component({
  selector: 'app-validation-category-bulk-import-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>$bulk-import Validation Categories</h2>
    <mat-dialog-content>
      <mat-form-field appearance="fill" class="full-width">
        <mat-label>JSON payload</mat-label>
        <textarea
          matInput
          [(ngModel)]="jsonText"
          rows="14"
          placeholder='Paste JSON for validation categories here...'
          aria-label="Bulk import JSON payload"></textarea>
      </mat-form-field>
    <div *ngIf="errorMessage" class="error-message">
        {{ errorMessage }}
    </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-raised-button color="primary" type="button" (click)="submit()">Import</button>
    </mat-dialog-actions>
  `,
  styles: [
    `.full-width { width: 100%; }
     .error-message { color: #b00020; margin-top: 0.75rem; font-size: 0.95rem; }`
  ]
})
export class ValidationCategoryBulkImportDialogComponent {
  jsonText = '';
  errorMessage = '';

  constructor(private dialogRef: MatDialogRef<ValidationCategoryBulkImportDialogComponent>) {}

  submit(): void {
    this.errorMessage = '';
    if (!this.jsonText.trim()) {
      this.errorMessage = 'Please paste JSON content before importing.';
      return;
    }

    try {
      const parsed = JSON.parse(this.jsonText);
      if (!Array.isArray(parsed)) {
        this.errorMessage = 'The payload must be a JSON array of category snapshots.';
        return;
      }
      this.dialogRef.close(parsed);
    } catch (error: unknown) {
      this.errorMessage = error instanceof Error ? error.message : 'Invalid JSON payload.';
    }
  }
}
