import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faCheck, faExclamationTriangle, faRefresh } from '@fortawesome/free-solid-svg-icons';
import { MatExpansionModule } from '@angular/material/expansion';
import { SftpAcquisitionLog } from '../models/sftp-acquisition-log';
import { SftpAcquisitionLogService } from '../sftp-acquisition-log.service';

@Component({
  selector: 'app-sftp-acquisition-log-details',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatExpansionModule
  ],
  templateUrl: './sftp-acquisition-log-details.component.html',
  styleUrls: ['./sftp-acquisition-log-details.component.scss']
})
export class SftpAcquisitionLogDetailsComponent implements OnInit {
  faXmark = faXmark;
  faCheck = faCheck;
  faExclamationTriangle = faExclamationTriangle;
  faRefresh = faRefresh;

  title = '';
  log!: SftpAcquisitionLog;
  isResetting = false;

  constructor(
    public dialogRef: MatDialogRef<SftpAcquisitionLogDetailsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      sftpAcquisitionLog: SftpAcquisitionLog
    },
    private sftpLogService: SftpAcquisitionLogService
  ) { }

  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.log = this.data.sftpAcquisitionLog;
  }

  isResettable(): boolean {
    return this.log.status === 'ConfigurationRequired' || this.log.status === 'MaxRetriesReached';
  }

  resetLog(): void {
    this.isResetting = true;
    this.sftpLogService.resetSftpAcquisitionLog(this.log.externalId).subscribe({
      next: () => {
        this.dialogRef.close({ reset: true });
      },
      error: (error) => {
        console.error('Error resetting SFTP acquisition log:', error);
        this.isResetting = false;
      }
    });
  }

  formatDuration(ms: number): string {
    if (ms < 1000) return `${ms.toFixed(0)}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`;
    return `${(ms / 60000).toFixed(2)}m`;
  }

  onClose(): void {
    this.dialogRef.close();
  }
}
