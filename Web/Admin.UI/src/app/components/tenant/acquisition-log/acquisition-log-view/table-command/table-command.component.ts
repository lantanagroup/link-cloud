import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faEllipsisV, faInfoCircle, faPlay } from '@fortawesome/free-solid-svg-icons';
import { AcquisitionLogDetailsComponent } from '../../acquisition-log-details/acquisition-log-details.component';
import { AcquisitionLog } from '../../models/acquisition-log';
import { AcquisitionLogService } from '../../acquisition-log.service';
import { ClickOutsideDirective } from 'src/app/directives/click-outside.directive';

@Component({
  selector: 'app-table-command',
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatDialogModule,
    ClickOutsideDirective
  ],  
  templateUrl: './table-command.component.html',
  styleUrl: './table-command.component.scss'
})
export class TableCommandComponent {
  @Input() acquisitionLogId!: string;

  faEllipsisV = faEllipsisV;
  faInfoCircle = faInfoCircle;
  faPlay = faPlay;
  isOpen = false;

  constructor(
    private dialog: MatDialog,
    private acquisitionLogService: AcquisitionLogService) { }

  toggleMenu() {
    this.isOpen = !this.isOpen;
  }

  showLogDetails() {    
    this.isOpen = false;

    //get acquisitin log details
    this.acquisitionLogService.getAcquisitionLog(this.acquisitionLogId).subscribe({
      next: (response) => {
        this.openLogDetails(response);
      },
      error: (error) => {
        console.error('Error loading acquisition log details:', error);
      }
    });       
  }

  openLogDetails(log: AcquisitionLog): void {
      
      const dialogConfig = new MatDialogConfig();
      dialogConfig.minWidth = '90vw';
      dialogConfig.maxHeight = '85vh';
      dialogConfig.panelClass = 'link-dialog-container';
      dialogConfig.data = {
        dialogTitle: 'Acquisition Log Details',
        acquisitionLog: log,      
      };
  
      this.dialog.open(AcquisitionLogDetailsComponent, dialogConfig);
    }

  executeLog() {
    this.isOpen = false;

    // Implement the logic to execute the log
    console.log('Executing log with ID:', this.acquisitionLogId);
  }

}
