import { Component, OnInit, OnDestroy } from '@angular/core';
import * as moment from 'moment-timezone';

import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { IFacilityConfigModel, PagedFacilityConfigModel } from '../../../interfaces/tenant/facility-config-model.interface';
import { IVendor } from '../../../interfaces/tenant/vendor-interface';
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
import { catchError, debounceTime, distinctUntilChanged, map, startWith, switchMap, take, tap } from 'rxjs/operators';
import { throwError, EMPTY, concat, Observable, of, Subject, Subscription } from 'rxjs';
import { AggregationService } from '../../../services/gateway/aggregation/aggregation.service';
import { MatCheckbox } from "@angular/material/checkbox";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { FormControl, FormsModule, ReactiveFormsModule } from "@angular/forms";
import { AsyncPipe, NgFor, NgIf } from "@angular/common";

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
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    FormsModule,
    ReactiveFormsModule,
    AsyncPipe,
    NgFor,
    NgIf
  ],
  templateUrl: './tenant-dashboard.component.html',
  styleUrls: ['./tenant-dashboard.component.scss']
})
export class TenantDashboardComponent implements OnInit, OnDestroy {
  private initPageSize: number = 10;
  private initPageNumber: number = 0;

  facilities: IFacilityConfigModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //displayedColumns: string[] = [ 'facilityId', 'facilityName', 'timeZone', 'Actions' ];
  dataSource = new MatTableDataSource<IFacilityConfigModel>(this.facilities);
  showDeleted = false;

  vendorOptions: IVendor[] = [];
  readonly timezoneOptions: string[] = moment.tz.names();

  // Facility autocomplete
  facilityInputControl = new FormControl<string>('');
  selectedFacilityId: string | null = null;
  filteredFacilities: Observable<{ facilityId: string; facilityName: string }[]> = of([]);
  private facilityAutocompleteSubscription: Subscription | undefined;

  //search parameters
  filterTimeZone: string = '';
  filterVendor: IVendor | null = null;
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

    this.filteredFacilities = this.facilityInputControl.valueChanges.pipe(
      startWith(''),
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => {
        const search = typeof term === 'string' ? term : '';
        return this.tenantService.autocompleteFacilities(search, this.showDeleted);
      }),
      map(results => Object.entries(results || {}).map(([facilityId, facilityName]) => ({ facilityId, facilityName: facilityName as string })))
    );

    this.tenantService.getVendors().subscribe(vendors => {
      this.vendorOptions = vendors;
    });

    this.getFacilities();
  }

  ngOnDestroy(): void {
    this.facilityAutocompleteSubscription?.unsubscribe();
  }

  getFacilities() {
    const facilityIdParam = this.selectedFacilityId ?? '';
    const facilityNameParam = !this.selectedFacilityId ? (this.facilityInputControl.value || '') : '';
    this.tenantService.listFacilities(
      facilityIdParam,
      facilityNameParam,
      this.filterTimeZone,
      this.filterVendor?.id ?? '',
      this.sortBy,
      this.sortOrder,
      this.paginationMetadata.pageSize,
      this.paginationMetadata.pageNumber,
      this.showDeleted).subscribe((facilities: PagedFacilityConfigModel) => {
        if (!facilities) {
          this.facilities = [];
          this.dataSource.data = [];
          this.paginationMetadata.totalCount = 0;
          this.paginationMetadata.totalPages = 0;
          return;
        }
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

  onFacilityInput(): void {
    this.selectedFacilityId = null;
    this.onSearchChange();
  }

  onFacilitySelected(fac: { facilityId: string; facilityName: string }): void {
    this.selectedFacilityId = fac.facilityId;
    this.facilityInputControl.setValue(fac.facilityName || fac.facilityId, { emitEvent: false });
    this.onSearchChange();
  }

  clearFacilityFilter(): void {
    this.selectedFacilityId = null;
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.onSearchChange();
  }

  displayFacility(fac: { facilityId: string; facilityName: string } | string | null): string {
    if (!fac) return '';
    if (typeof fac === 'string') return fac;
    return fac.facilityName || fac.facilityId;
  }

  trackFacility(_: number, fac: { facilityId: string }): string {
    return fac.facilityId;
  }

  clearFilters(): void {
    this.selectedFacilityId = null;
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.filterTimeZone = '';
    this.filterVendor = null;
    this.showDeleted = false;
    this.paginationMetadata.pageNumber = 0;
    this.getFacilities();
  }

  hasActiveFilters(): boolean {
    return !!(this.selectedFacilityId || this.facilityInputControl.value ||
              this.filterTimeZone || this.filterVendor || this.showDeleted);
  }

  onShowDeletedChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.getFacilities();
  }

  onSearchChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.getFacilities();
  }

  onSortChange(sort: Sort): void {
    const sortFieldMap: { [key: string]: string } = {
      'facilityId': 'FacilityId',
      'facilityName': 'FacilityName',
      'timeZone': 'TimeZone',
      'vendor': 'Vendor',
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
