import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AcquisitionLog } from '../models/acquisition-log';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark } from '@fortawesome/free-solid-svg-icons';
import { DonutChartComponent } from "../../../core/donut-chart/donut-chart.component";
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';

export interface AcquiredResourcesTable {
  resourceType: string;
  resourceId: string;
} 

export interface ReferencedResourcesTable extends AcquiredResourcesTable {
  phase: string;  
}

@Component({
  selector: 'app-acquisition-log-details',
  imports: [
    CommonModule,
    FontAwesomeModule,
    DonutChartComponent,
    MatPaginatorModule
],
  templateUrl: './acquisition-log-details.component.html',
  styleUrl: './acquisition-log-details.component.scss'
})
export class AcquisitionLogDetailsComponent implements OnInit {
  faXmark = faXmark;
  
  title: string = '';
  acquisitionLog!: AcquisitionLog;
  acquiredResourceRecords: Record<string, number> = {};
  acquiredResourceTable: AcquiredResourcesTable[] = [];
  acquiredResourceTableView: AcquiredResourcesTable[] = [];
  acquiredPaginationMetadata: PaginationMetadata = new PaginationMetadata;
  referenceResourceRecords: Record<string, number> = {};
  referenceResourceTable: ReferencedResourcesTable[] = [];
  referencedPaginationMetadata: PaginationMetadata = new PaginationMetadata;
  referenceResourceTableView: ReferencedResourcesTable[] = [];

  defaultPageNumber: number = 0;
  defaultPageSize: number = 5;

  constructor(
    public dialogRef: MatDialogRef<AcquisitionLogDetailsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, acquisitionLog: AcquisitionLog }, 
  ) { }


  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.acquisitionLog = this.data.acquisitionLog;
    this.acquiredResourceRecords = this.getAcquiredResourceRecords();
    this.referenceResourceRecords = this.getReferenceResourceRecords();

    this.acquiredResourceTableView = this.acquiredResourceTable.slice(0, this.defaultPageSize);
    this.acquiredPaginationMetadata = {
      pageNumber: this.defaultPageNumber,
      pageSize: this.defaultPageSize,
      totalCount: this.acquisitionLog.resourcesAcquired?.length || 0,
      totalPages: Math.ceil((this.acquisitionLog.resourcesAcquired?.length || 0) / this.defaultPageSize)
    };

    this.referenceResourceTableView = this.referenceResourceTable.slice(0, this.defaultPageSize);
    this.referencedPaginationMetadata = {
      pageNumber: this.defaultPageNumber,
      pageSize: this.defaultPageSize,
      totalCount: this.acquisitionLog.referencedResources?.length || 0,
      totalPages: Math.ceil((this.acquisitionLog.referencedResources?.length || 0) / this.defaultPageSize)
    };

  } 

  getAcquiredResourceRecords(): Record<string, number> {
    const acquiredResourceRecords: Record<string, number> = {};
    this.acquisitionLog.resourcesAcquired?.forEach(record => {
      
      let resource = record.split('/');
      const resourceType = resource[0];
      const resourceId = resource[1];    

      if (acquiredResourceRecords[resourceType]) {
        acquiredResourceRecords[resourceType] += 1;
      } else {
        acquiredResourceRecords[resourceType] = 1;
      }

      this.acquiredResourceTable.push({
        resourceType: resourceType,
        resourceId: resourceId
      });    

    });

    return acquiredResourceRecords;
  }

  getReferenceResourceRecords(): Record<string, number> {
    const referenceResourceRecords: Record<string, number> = {};
    this.acquisitionLog.referencedResources?.forEach(record => {
      
      let resource = record.identifier.split('/');
      const resourceType = resource[0];
      const resourceId = resource[1]; 

      if (referenceResourceRecords[resourceType]) {
        referenceResourceRecords[resourceType] += 1;
      } else {
        referenceResourceRecords[resourceType] = 1;
      }

      this.referenceResourceTable.push({
        resourceType: resourceType,
        resourceId: resourceId,
        phase: record.queryPhase
      });

    });    

    return referenceResourceRecords;
  }

  acquiredPagedEvent(event: PageEvent) {
    this.acquiredPaginationMetadata.pageSize = event.pageSize;
    this.acquiredPaginationMetadata.pageNumber = event.pageIndex; 
    
    const startIndex = (event.pageIndex - 1) * event.pageSize;
    this.acquiredResourceTableView = this.acquiredResourceTable.slice(startIndex, startIndex + event.pageSize);    
  }

  referencePagedEvent(event: PageEvent) {
    this.referencedPaginationMetadata.pageSize = event.pageSize;
    this.referencedPaginationMetadata.pageNumber = event.pageIndex; 
    
    const startIndex = (event.pageIndex - 1) * event.pageSize;
    this.referenceResourceTableView = this.referenceResourceTable.slice(startIndex, startIndex + event.pageSize);    
  }

  onModalClose(): void {
    this.dialogRef.close();
  }

}
