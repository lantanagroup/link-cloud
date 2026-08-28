import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EMPTY, Subject, Subscription, catchError, switchMap } from 'rxjs';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FormsModule } from '@angular/forms';

import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { FormMode } from '../../../models/FormMode.enum';
import { DeleteConfirmationDialogComponent } from '../../core/delete-confirmation-dialog/delete-confirmation-dialog.component';
import { MeasureMappingDialogComponent } from '../measure-mapping-dialog/measure-mapping-dialog.component';
import { MeasureMappingService } from '../../../services/gateway/dmrp/measure-mapping.service';
import { Frequency, IMeasureMapping, MEASURE_MAPPING_FREQUENCIES } from '../../../interfaces/dmrp/measure-mapping.interface';

@Component({
  selector: 'app-measure-mappings-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    FormsModule
  ],
  templateUrl: './measure-mappings-dashboard.component.html',
  styleUrls: ['./measure-mappings-dashboard.component.scss']
})
export class MeasureMappingsDashboardComponent implements OnInit, OnDestroy {
  private initPageSize = 10;
  private initPageNumber = 0;

  private readonly search$ = new Subject<void>();
  private searchSubscription?: Subscription;

  measureMappings: IMeasureMapping[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata();
  dataSource = new MatTableDataSource<IMeasureMapping>([]);
  displayedColumns: string[] = ['measure', 'dqm', 'frequency', 'actions'];

  readonly frequencyOptions = MEASURE_MAPPING_FREQUENCIES;

  filterMeasure = '';
  filterDqm = '';
  filterFrequency: Frequency | null = null;
  sortBy = 'Measure';
  sortOrder = 0;

  loading = false;

  constructor(
    private measureMappingService: MeasureMappingService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {
  }

  ngOnInit(): void {
    this.paginationMetadata.pageNumber = this.initPageNumber;
    this.paginationMetadata.pageSize = this.initPageSize;

    // Every reload goes through this one stream: switchMap drops the in-flight request when a
    // newer search starts, so a slow earlier response can never overwrite a later one's results.
    // Errors are caught per-request, or the first failure would end the stream for good.
    this.searchSubscription = this.search$.pipe(
      switchMap(() => this.measureMappingService.searchMeasureMappings(
        this.filterMeasure,
        this.filterDqm,
        this.filterFrequency,
        this.sortBy,
        this.sortOrder,
        this.paginationMetadata.pageSize,
        this.paginationMetadata.pageNumber
      ).pipe(
        catchError(() => {
          this.loading = false;
          return EMPTY;
        })
      ))
    ).subscribe((response) => {
      this.loading = false;

      if (!response) {
        this.measureMappings = [];
        this.dataSource.data = [];
        this.paginationMetadata.totalCount = 0;
        this.paginationMetadata.totalPages = 0;
        return;
      }

      this.measureMappings = response.records;
      this.dataSource.data = this.measureMappings;
      this.paginationMetadata = response.metadata;
    });

    this.getMeasureMappings();
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
    this.search$.complete();
  }

  getMeasureMappings(): void {
    this.loading = true;
    this.search$.next();
  }

  onSearchChange(): void {
    this.paginationMetadata.pageNumber = 0;
    this.getMeasureMappings();
  }

  onSortChange(sort: Sort): void {
    const sortFieldMap: { [key: string]: string } = {
      measure: 'Measure',
      dqm: 'DQM',
      frequency: 'Frequency'
    };

    if (sort.active && sort.direction) {
      this.sortBy = sortFieldMap[sort.active] || 'Measure';
      this.sortOrder = sort.direction === 'desc' ? 1 : 0;
    } else {
      this.sortBy = 'Measure';
      this.sortOrder = 0;
    }

    this.paginationMetadata.pageNumber = 0;
    this.getMeasureMappings();
  }

  pagedEvent(event: PageEvent): void {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.getMeasureMappings();
  }

  clearFilters(): void {
    this.filterMeasure = '';
    this.filterDqm = '';
    this.filterFrequency = null;
    this.paginationMetadata.pageNumber = 0;
    this.getMeasureMappings();
  }

  hasActiveFilters(): boolean {
    return !!(this.filterMeasure || this.filterDqm || this.filterFrequency);
  }

  onAdd(): void {
    this.dialog.open(MeasureMappingDialogComponent, {
      width: '75%',
      data: {
        dialogTitle: 'Add Measure Mapping',
        formMode: FormMode.Create,
        viewOnly: false,
        measureMapping: {}
      }
    }).afterClosed().subscribe(res => {
      if (res) {
        this.getMeasureMappings();
        this.snackBar.open('Measure Mapping Created', '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  onEdit(row: IMeasureMapping): void {
    this.dialog.open(MeasureMappingDialogComponent, {
      width: '75%',
      data: {
        dialogTitle: 'Edit Measure Mapping',
        formMode: FormMode.Edit,
        viewOnly: false,
        measureMapping: row
      }
    }).afterClosed().subscribe(res => {
      if (res) {
        this.getMeasureMappings();
        this.snackBar.open('Measure Mapping Updated', '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  onDelete(row: IMeasureMapping): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '400px',
      data: {
        message: `Are you sure you want to delete the mapping for measure "${row.measure}"?`
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.measureMappingService.deleteMeasureMapping(row.id).subscribe(() => {
          this.snackBar.open('Measure mapping deleted successfully!', '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.getMeasureMappings();
        });
      }
    });
  }
}
