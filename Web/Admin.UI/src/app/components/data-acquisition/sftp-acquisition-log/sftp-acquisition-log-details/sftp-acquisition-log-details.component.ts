import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faFile, faClock, faCheck, faExclamationTriangle } from '@fortawesome/free-solid-svg-icons';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatButtonModule } from '@angular/material/button';
import { SftpAcquisitionLog } from '../models/sftp-acquisition-log';

@Component({
  selector: 'app-sftp-acquisition-log-details',
  standalone: true,
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatDialogModule,
    MatExpansionModule,
    MatButtonModule
  ],
  templateUrl: './sftp-acquisition-log-details.component.html',
  styleUrls: ['./sftp-acquisition-log-details.component.scss']
})
export class SftpAcquisitionLogDetailsComponent implements OnInit {
  faXmark = faXmark;
  faFile = faFile;
  faClock = faClock;
  faCheck = faCheck;
  faExclamationTriangle = faExclamationTriangle;

  title = '';
  log!: SftpAcquisitionLog;

  constructor(
    public dialogRef: MatDialogRef<SftpAcquisitionLogDetailsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: {
      dialogTitle: string,
      sftpAcquisitionLog: SftpAcquisitionLog
    }
  ) { }

  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.log = this.data.sftpAcquisitionLog;
  }

  getStatusClass(status: string): string {
    const statusMap: Record<string, string> = {
      'Pending': 'status-pending',
      'Processing': 'status-processing',
      'Completed': 'status-completed',
      'Failed': 'status-failed',
      'MaxRetriesReached': 'status-maxRetryReached'
    };
    return statusMap[status] || '';
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
