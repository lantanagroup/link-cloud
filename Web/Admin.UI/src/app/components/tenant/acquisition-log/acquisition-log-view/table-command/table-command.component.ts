import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { MatDialog, MatDialogConfig, MatDialogModule } from '@angular/material/dialog';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faBan, faEllipsisV, faInfoCircle, faPlay } from '@fortawesome/free-solid-svg-icons';
import { AcquisitionLogDetailsComponent } from '../../acquisition-log-details/acquisition-log-details.component';
import { AcquisitionLog } from '../../models/acquisition-log';
import { AcquisitionLogService } from '../../acquisition-log.service';
import { ClickOutsideDirective } from 'src/app/directives/click-outside.directive';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SnackbarHelper } from 'src/app/services/snackbar-helper';
import { Subscription } from 'rxjs';
import { animate, style, transition, trigger } from '@angular/animations';

@Component({
  selector: 'app-table-command',
  imports: [
    CommonModule,
    FontAwesomeModule,
    MatDialogModule,
    ClickOutsideDirective
  ],  
  templateUrl: './table-command.component.html',
  styleUrl: './table-command.component.scss',
  animations: [
    trigger('dropdownAnimation', [
      transition(':enter', [
        style({ transform: 'scale(0.95)', opacity: 0 }),
        animate('100ms ease-out', style({ transform: 'scale(1)', opacity: 1 }))
      ]),
      transition(':leave', [
        animate('75ms ease-in', style({ transform: 'scale(0.95)', opacity: 0 }))
      ])
    ])
  ]
})
export class TableCommandComponent implements OnInit, OnDestroy {
  @Input() acquisitionLogId!: string;
  @Input() priority: string | undefined;
  @Input() status: string = '';
  @Input() createDate: string | Date | undefined;
  @Input() cancelMinAgeHours: number = 0;

  @Output() queryLogAddedToQueue = new EventEmitter<string>();
  @Output() queryLogCancelled = new EventEmitter<string>();

  faEllipsisV = faEllipsisV;
  faInfoCircle = faInfoCircle;
  faPlay = faPlay;
  faBan = faBan;
  isOpen = false;

  get isCancelEligible(): boolean {
    if (['MaxRetriesReached', 'ConfigurationMissing', 'Completed', 'Cancelled', 'Skipped'].includes(this.status)) return false;
    if (this.cancelMinAgeHours === 0) return true;
    if (!this.createDate) return false;
    // Match backend: CreateDate <= UtcNow.AddHours(-minAgeHours)
    const minAgeCutoff = Date.now() - (this.cancelMinAgeHours * 60 * 60 * 1000);
    return new Date(this.createDate).getTime() <= minAgeCutoff;
  }

  private subscription = new Subscription();
  
  constructor(
    private dialog: MatDialog,
    private acquisitionLogService: AcquisitionLogService,
    private snackBar: MatSnackBar) { } 

  ngOnInit(): void {
    if(!this.acquisitionLogId) {
      throw new Error('Acquisition Log Id is required');
    }
  }  

  toggleMenu() {
    this.isOpen = !this.isOpen;
  }

  showLogDetails() {    
    this.isOpen = false;

    //get acquisitin log details
    this.subscription.add(
      this.acquisitionLogService.getAcquisitionLog(this.acquisitionLogId).subscribe({
        next: (response) => {
          this.openLogDetails(response);
        },
        error: (error) => {
          console.error('Failed to load acquisition log details:', error);
        }
      })
    );       
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

    //check of query log id is set
    if(this.acquisitionLogId !== undefined) {
      console.log(`Execute command: for query log id: ${this.acquisitionLogId}`);

      // add the query to the execution queue
      this.acquisitionLogService.executeAcquisitionLog(this.acquisitionLogId).subscribe(() => {
        this.queryLogAddedToQueue.emit(this.acquisitionLogId);
        console.log(`Query log id: ${this.acquisitionLogId} added to the execution queue.`);
        this.isOpen = false;
      });
    }
    else
    {
      console.log(`Query log id is not set.`);
    }
    
  }

  cancelLog() {
    this.isOpen = false;
    this.acquisitionLogService.cancelBulkAcquisitionLogs([this.acquisitionLogId], this.cancelMinAgeHours).subscribe({
      next: (result) => {
        if (result?.cancelled > 0) {
          SnackbarHelper.showSuccessMessage(this.snackBar, 'Acquisition log cancelled.');
          this.queryLogCancelled.emit(this.acquisitionLogId);
        } else {
          SnackbarHelper.showErrorMessage(this.snackBar, 'Log was not cancelled (terminal status or too recent).');
        }
      },
      error: () => {
        SnackbarHelper.showErrorMessage(this.snackBar, 'Error cancelling acquisition log.');
      }
    });
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

}
