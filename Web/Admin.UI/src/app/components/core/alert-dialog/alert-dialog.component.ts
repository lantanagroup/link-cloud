import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

export interface AlertDialogData {
  title: string;
  /**
   * SECURITY: When {@link isHtml} is true, `message` is bound via [innerHTML].
   * Angular sanitizes scripts and event handlers, but passive HTML passes through.
   * Only use isHtml with hardcoded strings — never with user input or API responses.
   */
  message: string;
  icon?: string;
  iconColor?: string;
  isHtml?: boolean;
}

@Component({
  selector: 'app-alert-dialog',
  templateUrl: './alert-dialog.component.html',
  styleUrls: ['./alert-dialog.component.scss'],
  imports: [MatIconModule, MatButtonModule]
})
export class AlertDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<AlertDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AlertDialogData
  ) {}

  get icon(): string {
    return this.data.icon || 'warning';
  }

  get iconColor(): string {
    return this.data.iconColor || 'warn';
  }

  onClose(): void {
    this.dialogRef.close();
  }
}
