import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";

export interface ConfirmationDialogData {
  message: string;
  title?: string;
  confirmButtonText?: string;
  icon?: string;
  iconColor?: string;
}

@Component({
  selector: 'app-delete-confirmation-dialog',
  templateUrl: './delete-confirmation-dialog.component.html',
  styleUrls: ['./delete-confirmation-dialog.component.scss'],
  imports: [
    MatIconModule,
    MatButtonModule
  ]
})
export class DeleteConfirmationDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<DeleteConfirmationDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ConfirmationDialogData
  ) {}

  get title(): string {
    return this.data.title || 'Delete Confirmation';
  }

  get confirmButtonText(): string {
    return this.data.confirmButtonText || 'Delete';
  }

  get icon(): string {
    return this.data.icon || 'warning';
  }

  get iconColor(): string {
    return this.data.iconColor || 'warn';
  }

  onCancel(): void {
    this.dialogRef.close(false); // User clicked cancel
  }

  onConfirm(): void {
    this.dialogRef.close(true); // User confirmed action
  }
}
