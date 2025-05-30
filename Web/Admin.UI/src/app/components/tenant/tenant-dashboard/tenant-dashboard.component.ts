import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IFacilityConfigModel, PagedFacilityConfigModel } from '../../../interfaces/tenant/facility-config-model.interface';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FacilityConfigDialogComponent } from '../facility-config-dialog/facility-config-dialog.component';
import { RouterLink } from '@angular/router';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import {MatPaginatorModule, PageEvent} from "@angular/material/paginator";

@Component({
  selector: 'app-tenant-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    RouterLink,
    MatPaginatorModule,
    MatIconModule
  ],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss']
})
export class TenantDashboardComponent implements OnInit {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  facilities: IFacilityConfigModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  displayedColumns: string[] = [ 'facilityId', 'facilityName', 'scheduledTasks', 'Actions' ];
  dataSource = new MatTableDataSource<IFacilityConfigModel>(this.facilities);


  //search parameters
  filterFacilityBy: string = '';
  filterFacilityName: string = '';
  sortBy: string = 'FacilityId';
  sortOrder: number = 0;

  constructor(private tenantService: TenantService, private dialog: MatDialog, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dataSource = new MatTableDataSource<IFacilityConfigModel>();
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;
    this.getFacilities();
  }

  getFacilities() {
    this.tenantService.listFacilities(
      this.filterFacilityBy,
      this.filterFacilityName,
      this.sortBy,
      this.sortOrder,
      this.paginationMetadata.pageSize,
      this.paginationMetadata.pageNumber).subscribe((facilities: PagedFacilityConfigModel) => {
      this.facilities = facilities.records;
      this.dataSource.data = this.facilities;
      this.paginationMetadata = facilities.metadata;
    });
  }

  showCreateFacilityDialog(): void {
    this.dialog.open(FacilityConfigDialogComponent,
      {
        width: '75%',
        data: { dialogTitle: 'Create a facility configuration', viewOnly: false, facilityConfig: null }
      }).afterClosed().subscribe(res => {
        console.log(res)
        if (res) {
          this.getFacilities();
          this.snackBar.open(`${res}`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getFacilities();
  }

  deleteTenant() {
    alert('TODO');
  }
}
