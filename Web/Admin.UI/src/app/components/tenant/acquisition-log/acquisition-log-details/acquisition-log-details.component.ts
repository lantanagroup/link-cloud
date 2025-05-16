import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AcquisitionLog } from '../models/acquisition-log';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark } from '@fortawesome/free-solid-svg-icons';

@Component({
  selector: 'app-acquisition-log-details',
  imports: [
    CommonModule,
    FontAwesomeModule
  ],
  templateUrl: './acquisition-log-details.component.html',
  styleUrl: './acquisition-log-details.component.scss'
})
export class AcquisitionLogDetailsComponent implements OnInit {
  faXmark = faXmark;
  
  title: string = '';
  acquisitionLog!: AcquisitionLog;

  constructor(
    public dialogRef: MatDialogRef<AcquisitionLogDetailsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, acquisitionLog: AcquisitionLog }, 
  ) { }


  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.acquisitionLog = this.data.acquisitionLog;
  } 

  onModalClose(): void {
    this.dialogRef.close();
  }

}
