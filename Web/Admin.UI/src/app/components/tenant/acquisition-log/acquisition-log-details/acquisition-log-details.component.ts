import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AcquisitionLog } from '../models/acquisition-log';
import { CommonModule } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faXmark, faSearch } from '@fortawesome/free-solid-svg-icons';
import { DonutChartComponent } from "../../../core/donut-chart/donut-chart.component";
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { PaginationMetadata } from 'src/app/models/pagination-metadata.model';
import { FormsModule } from '@angular/forms';
import { debounceTime, Subject, Subscription } from 'rxjs';

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
    MatPaginatorModule,
    FormsModule
],
  templateUrl: './acquisition-log-details.component.html',
  styleUrl: './acquisition-log-details.component.scss'
})
export class AcquisitionLogDetailsComponent implements OnInit {
  faXmark = faXmark;
  faSearch = faSearch;
  
  title: string = '';
  acquisitionLog!: AcquisitionLog;
  acquiredResourceRecords: Record<string, number> = {};
  acquiredResourceTable: AcquiredResourcesTable[] = [];
  filteredAcquiredResourceTable: AcquiredResourcesTable[] = [];
  acquiredResourceTableView: AcquiredResourcesTable[] = [];
  acquiredPaginationMetadata: PaginationMetadata = new PaginationMetadata;
  referenceResourceRecords: Record<string, number> = {};
  referenceResourceTable: ReferencedResourcesTable[] = [];
  filteredReferenceResourceTable: ReferencedResourcesTable[] = [];
  referencedPaginationMetadata: PaginationMetadata = new PaginationMetadata;
  referenceResourceTableView: ReferencedResourcesTable[] = [];

  defaultPageNumber: number = 0;
  defaultPageSize: number = 5;
  acquiredSearchText: string = '';
  referenceSearchText: string = '';  

  private searchAcquisitionSubject = new Subject<string>();
  private searchAcquisitionSub!: Subscription;

  private searchReferenceSubject = new Subject<string>();
  private searchReferenceSub!: Subscription;

  constructor(
    public dialogRef: MatDialogRef<AcquisitionLogDetailsComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { dialogTitle: string, acquisitionLog: AcquisitionLog }, 
  ) { }


  ngOnInit(): void {
    this.title = this.data.dialogTitle;
    this.acquisitionLog = this.data.acquisitionLog;
    this.acquiredResourceRecords = this.getAcquiredResourceRecords();
    this.filteredAcquiredResourceTable = [...this.acquiredResourceTable];
    this.referenceResourceRecords = this.getReferenceResourceRecords();
    this.filteredReferenceResourceTable = [...this.referenceResourceTable];

    this.acquiredResourceTableView = this.filteredAcquiredResourceTable.slice(0, this.defaultPageSize);
    this.acquiredPaginationMetadata = {
      pageNumber: this.defaultPageNumber,
      pageSize: this.defaultPageSize,
      totalCount: this.filteredAcquiredResourceTable?.length || 0,
      totalPages: Math.ceil((this.filteredAcquiredResourceTable?.length || 0) / this.defaultPageSize)
    };

    this.referenceResourceTableView = this.filteredReferenceResourceTable.slice(0, this.defaultPageSize);
    this.referencedPaginationMetadata = {
      pageNumber: this.defaultPageNumber,
      pageSize: this.defaultPageSize,
      totalCount: this.filteredReferenceResourceTable?.length || 0,
      totalPages: Math.ceil((this.filteredReferenceResourceTable?.length || 0) / this.defaultPageSize)
    };

    this.searchAcquisitionSub = this.searchAcquisitionSubject
      .pipe(debounceTime(300))
      .subscribe(searchText => {
        const text = searchText.toLowerCase();
        this.filteredAcquiredResourceTable = this.acquiredResourceTable.filter(item =>      
            item.resourceId.toLowerCase().includes(text) || item.resourceType.toLowerCase().includes(text)          
        );

        this.acquiredPaginationMetadata.totalCount = this.filteredAcquiredResourceTable.length || 0;
        this.acquiredPaginationMetadata.totalPages = Math.ceil((this.filteredAcquiredResourceTable.length || 0) / this.defaultPageSize);

        this.acquiredResourceTableView = this.filteredAcquiredResourceTable.slice(0, this.defaultPageSize);
      });

    this.searchReferenceSub = this.searchReferenceSubject
      .pipe(debounceTime(300))
      .subscribe(searchText => {
        const text = searchText.toLowerCase();
        this.filteredReferenceResourceTable = this.referenceResourceTable.filter(item =>         
            item.resourceId.toLowerCase().includes(text) || item.resourceType.toLowerCase().includes(text)          
        );

        this.referencedPaginationMetadata.totalCount = this.filteredReferenceResourceTable.length || 0;
        this.referencedPaginationMetadata.totalPages = Math.ceil((this.filteredReferenceResourceTable.length || 0) / this.defaultPageSize);

        this.referenceResourceTableView = this.filteredReferenceResourceTable.slice(0, this.defaultPageSize);
      });
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
    
    const startIndex = event.pageIndex * event.pageSize;   
    this.acquiredResourceTableView = this.filteredAcquiredResourceTable.slice(startIndex, startIndex + event.pageSize);
  }    

  referencePagedEvent(event: PageEvent) {
    this.referencedPaginationMetadata.pageSize = event.pageSize;
    this.referencedPaginationMetadata.pageNumber = event.pageIndex; 
    
    const startIndex = event.pageIndex * event.pageSize;
     
    this.referenceResourceTableView = this.filteredReferenceResourceTable.slice(startIndex, startIndex + event.pageSize);   
  }

  onAcquiredResourceSearch(text: string) {
    this.searchAcquisitionSubject.next(text);
  }

  onReferenceResourceSearch(text: string) {
    this.searchReferenceSubject.next(text);
  }
 

  onModalClose(): void {
    this.dialogRef.close();
  }

}
