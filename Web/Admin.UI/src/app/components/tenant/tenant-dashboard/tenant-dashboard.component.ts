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
import { MatSortModule, Sort } from '@angular/material/sort';
import { CensusService } from "../../../services/gateway/census/census.service";
import { DataAcquisitionService } from "../../../services/gateway/data-acquisition/data-acquisition.service";
import { QueryDispatchService } from "../../../services/gateway/query-dispatch/query-dispatch.service";
import { OperationService } from "../../../services/gateway/normalization/operation.service";
import { DeleteConfirmationDialogComponent } from "../../core/delete-confirmation-dialog/delete-confirmation-dialog.component";
import { AlertDialogComponent } from "../../core/alert-dialog/alert-dialog.component";
import { catchError, take } from 'rxjs/operators';
import { throwError, EMPTY, concat } from 'rxjs';
import { AggregationService } from '../../../services/gateway/aggregation/aggregation.service';
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
    MatSortModule,
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

  constructor(private tenantService: TenantService, private censusService: CensusService,
    private dataAcquisitionService: DataAcquisitionService,
    private queryDispatchService: QueryDispatchService,
    private operationService: OperationService,
    private aggregationService: AggregationService,
    private dialog: MatDialog, private snackBar: MatSnackBar) { }

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
      'vendor',
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

  onSortChange(sort: Sort): void {
    const sortFieldMap: { [key: string]: string } = {
      'facilityId': 'FacilityId',
      'facilityName': 'FacilityName',
      'timeZone': 'TimeZone',
      'isDeleted': 'IsDeleted'
    };

    if (sort.active && sort.direction) {
      this.sortBy = sortFieldMap[sort.active] || 'FacilityId';
      this.sortOrder = sort.direction === 'desc' ? 1 : 0;
    } else {
      this.sortBy = 'FacilityId';
      this.sortOrder = 0;
    }

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
        message: 'Are you sure you want to soft delete this facility and all related report schedules and acquisition logs?'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(confirmed => {
      if (!confirmed) return;

      const progressSnackBar = this.snackBar.open('Soft deleting facility, please wait...', 'Close');

      this.aggregationService.softDeleteFacility(facilityId).subscribe({
        next: () => {
          this.snackBar.open('Facility soft deleted successfully', 'Close', { duration: 3000, panelClass: 'success-snackbar' });
          this.getFacilities();
        },
        error: (err) => {
          console.error('Soft delete failed', err);
          progressSnackBar.dismiss();
          const detail = this.extractDetail(err);
          const is409 = err.status === 409;
          this.dialog.open(AlertDialogComponent, {
            width: '420px',
            data: {
              title: is409 ? 'Reports In Progress' : 'Soft Delete Failed',
              message: detail || (is409
                ? 'This tenant cannot be soft-deleted because there are reports currently in progress. Please wait for all reports to complete before trying again.'
                : 'Failed to soft delete the facility. Please try again.'),
              icon: is409 ? 'running_with_errors' : 'error',
              iconColor: 'warn'
            }
          });
        }
      });
    });
  }


  private extractDetail(err: any): string | null {
    if (!err.error) return null;
    if (typeof err.error === 'object') return err.error.detail ?? null;
    try { return JSON.parse(err.error)?.detail ?? null; } catch { return null; }
  }

  onRestoreFacility(facilityId: string): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: 'Are you sure you want to restore this facility and all related report schedules and acquisition logs?',
        confirmButtonText: 'Restore',
        title: 'Restore Facility',
        icon: 'restore',
        iconColor: 'accent'
      }
    });

    dialogRef.afterClosed().pipe(take(1)).subscribe(confirmed => {
      if (!confirmed) return;

      const progressSnackBar = this.snackBar.open('Restoring facility, please wait...', 'Close');

      this.aggregationService.restoreFacility(facilityId).subscribe({
        next: () => {
          this.snackBar.open('Facility restored successfully', 'Close', { duration: 3000, panelClass: 'success-snackbar' });
          this.getFacilities();
        },
        error: (err) => {
          console.error('Restore failed', err);
          progressSnackBar.dismiss();
          const detail = this.extractDetail(err);
          this.dialog.open(AlertDialogComponent, {
            width: '420px',
            data: {
              title: 'Restore Failed',
              message: detail || 'Failed to restore the facility. Please try again.',
              icon: 'error',
              iconColor: 'warn'
            }
          });
        }
      });
    });
  }
}
