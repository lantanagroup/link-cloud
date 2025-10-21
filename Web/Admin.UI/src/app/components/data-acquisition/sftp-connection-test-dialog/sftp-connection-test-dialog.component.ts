import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ISftpConnectionTestResult } from '../../../interfaces/data-acquisition/sftp-config-model.interface';

export interface SftpConnectionTestDialogData {
  isLoading: boolean;
  result?: ISftpConnectionTestResult;
  error?: string;
}

@Component({
  selector: 'app-sftp-connection-test-dialog',
  templateUrl: './sftp-connection-test-dialog.component.html',
  styleUrls: ['./sftp-connection-test-dialog.component.scss'],
  imports: [
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ]
})
export class SftpConnectionTestDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<SftpConnectionTestDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: SftpConnectionTestDialogData
  ) {}

  onClose(): void {
    this.dialogRef.close();
  }
}
