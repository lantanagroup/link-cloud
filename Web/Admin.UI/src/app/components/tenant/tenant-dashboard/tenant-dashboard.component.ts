import { Component, OnInit } from '@angular/core';

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
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { CensusService } from "../../../services/gateway/census/census.service";
import { DataAcquisitionService } from "../../../services/gateway/data-acquisition/data-acquisition.service";
import { QueryDispatchService } from "../../../services/gateway/query-dispatch/query-dispatch.service";
import { OperationService } from "../../../services/gateway/normalization/operation.service";
import { DeleteConfirmationDialogComponent } from "../../core/delete-confirmation-dialog/delete-confirmation-dialog.component";
import { catchError, concatMap, take } from 'rxjs/operators';
import { throwError, EMPTY, forkJoin, concat } from 'rxjs';
import { ReportService } from "../../../services/gateway/report/report.service";
import { MatCheckbox } from "@angular/material/checkbox";
import { FormsModule } from "@angular/forms";
import { NgIf } from "@angular/common";

@Component({
  selector: 'app-tenant-dashboard',
  standalone: true,
  imports: [
    MatDialogModule,
    MatTableModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatSnackBarModule,
    RouterLink,
    MatPaginatorModule,
    MatIconModule,
    MatCheckbox,
    FormsModule,
    NgIf
  ],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss']
})
export class TenantDashboardComponent implements OnInit {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  facilities: IFacilityConfigModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //displayedColumns: string[] = [ 'facilityId', 'facilityName', 'timeZone', 'Actions' ];
  dataSource = new MatTableDataSource<IFacilityConfigModel>(this.facilities);
  showDeleted = false;

  //search parameters
  filterFacilityBy: string = '';
  filterFacilityName: string = '';
  sortBy: string = 'FacilityId';
  sortOrder: number = 0;

  constructor(private tenantService: TenantService, private reportService: ReportService, private censusService: CensusService,
    private dataAcquisitionService: DataAcquisitionService,
    private queryDispatchService: QueryDispatchService,
    private operationService: OperationService, private dialog: MatDialog, private snackBar: MatSnackBar) { }

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
      this.paginationMetadata.pageNumber,
      this.showDeleted).subscribe((facilities: PagedFacilityConfigModel) => {
        this.facilities = facilities.records;
        this.dataSource.data = this.facilities;
        this.paginationMetadata = facilities.metadata;
      });
  }

  getColumns(): string[] {
    const columns = [
      'facilityId',
      'facilityName',
      'timeZone',
      'action'
    ];

    if (this.showDeleted) {
      columns.push('isDeleted');
    }

    return columns;
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

  onShowDeletedChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.getFacilities();
  }

  pagedEvent(event: PageEvent) {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getFacilities();
  }

  onDeleteFacility(facilityId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: 'Are you sure you want to delete this facility and all related configurations and operations?'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(result => {
      if (!result) return;

      this.snackBar.open('Deleting facility, please wait...', 'Close');

      // Helper to skip 404s
      const safeDelete = (obs: any) =>
        obs.pipe(
          catchError(err => {
            if (err.status === 404) {
              console.warn('Resource not found, skipping');
              return EMPTY;
            }
            else {
              return throwError(() => err);
            }
          })
        );

      // Build sequential deletion sequence
      concat(
        safeDelete(this.dataAcquisitionService.deleteAllQueryPlanConfiguration(facilityId)),
        safeDelete(this.dataAcquisitionService.deleteFhirListConfiguration(facilityId)),
        safeDelete(this.dataAcquisitionService.deleteFhirQueryConfiguration(facilityId)),
        safeDelete(this.censusService.deleteConfiguration(facilityId)),
        safeDelete(this.queryDispatchService.deleteConfiguration(facilityId)),
        safeDelete(this.operationService.deleteAllOperationsByFacility(facilityId)),
        safeDelete(this.tenantService.deleteFacilityConfiguration(facilityId))
      ).subscribe({
        next: () => { },
        complete: () => {
          this.snackBar.open('Facility and all related configurations deleted successfully', 'Close', { duration: 3000 });
          this.getFacilities();
        },
        error: (err) => {
          console.error('Deletion failed', err);
          this.snackBar.open('Failed to delete some configurations or operations', 'Close', { duration: 3000 });
        }
      });
    });
  }

  onSoftDeleteFacility(facilityId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: 'Are you sure you want to delete this facility and all related configurations and operations?'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(result => {
      if (!result) return;

      this.snackBar.open('Deleting facility, please wait...', 'Close');

      // Helper to skip 404s
      const safeDelete = (obs: any) =>
        obs.pipe(
          catchError(err => {
            if (err.status === 404) {
              console.warn('Resource not found, skipping');
              return EMPTY;
            }
            else {
              return throwError(() => err);
            }
          })
        );

      // Build sequential deletion sequence
      concat(
        safeDelete(this.tenantService.softDeleteFacilityConfiguration(facilityId)),
        safeDelete(this.reportService.deleteReports(facilityId)),
      ).subscribe({
        next: () => { },
        complete: () => {
          this.snackBar.open('Facility soft deleted', 'Close', { duration: 3000 });
          this.getFacilities();
        },
        error: (err) => {
          console.error('Deletion failed', err);
          this.snackBar.open('Failed to delete', 'Close', { duration: 3000 });
        }
      });
    });
  }
}
