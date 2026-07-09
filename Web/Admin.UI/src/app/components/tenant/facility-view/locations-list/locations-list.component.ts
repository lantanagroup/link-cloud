import {Component, Input, OnDestroy, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatPaginatorModule, PageEvent} from '@angular/material/paginator';
import {MatSortModule, Sort} from '@angular/material/sort';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatCheckboxModule} from '@angular/material/checkbox';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {Subject, Subscription} from 'rxjs';
import {debounceTime} from 'rxjs/operators';
import {DataAcquisitionService} from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import {SnackbarHelper} from '../../../../services/snackbar-helper';
import {PaginationMetadata} from '../../../../models/pagination-metadata.model';
import {IOrganizationLocationMappingModel} from '../../../../interfaces/data-acquisition/organization-location-mapping-model.interface';

// A tri-state filter value: '' means "Any" (no filter), otherwise a stringified boolean.
type TriStateFilter = '' | 'true' | 'false';

@Component({
  selector: 'app-locations-list',
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule
  ],
  templateUrl: './locations-list.component.html',
  styleUrl: './locations-list.component.scss'
})
export class LocationsListComponent implements OnInit, OnDestroy {

  @Input() facilityId: string = '';

  // locationMappingId (the DB primary key) leads as a copyable debugging aid for cross-referencing
  // backend logs; modifiedDate trails for "when did this change" investigations.
  displayedColumns = ['locationMappingId', 'locationId', 'locationName', 'locationAlias', 'partOfValue', 'partOfId', 'isOrgLocation', 'isActive', 'createDate', 'modifiedDate'];
  dataSource = new MatTableDataSource<IOrganizationLocationMappingModel>([]);
  paginationMetadata: PaginationMetadata = Object.assign(new PaginationMetadata(), {
    pageSize: 10,
    pageNumber: 0,
    totalCount: 0,
    totalPages: 0
  });

  // Free-text (partial match) filters, the IsOrgLocation tri-state, and the active toggle.
  // By default only active locations are shown; checking "Show inactive" includes inactive ones.
  locationIdFilter = '';
  locationNameFilter = '';
  locationAliasFilter = '';
  partOfValueFilter = '';
  isOrgLocationFilter: TriStateFilter = '';
  showInactive = false;

  // Undefined => let the backend apply its default sort (CreateDate desc).
  // sortOrder follows the API convention: 0 = Ascending, 1 = Descending.
  currentSortBy?: string;
  currentSortOrder?: number;

  // Maps sortable UI column names to the API field names the backend allows. Every displayed column
  // is a scalar column on the mapping row, so all are sortable.
  private readonly sortFieldMap: { [key: string]: string } = {
    locationMappingId: 'LocationMappingId',
    locationId: 'LocationId',
    locationName: 'LocationName',
    locationAlias: 'LocationAlias',
    partOfValue: 'PartOfValue',
    partOfId: 'PartOfId',
    isOrgLocation: 'IsOrgLocation',
    isActive: 'IsActive',
    createDate: 'CreateDate',
    modifiedDate: 'ModifiedDate'
  };

  isLoading = false;
  errorMessage: string | null = null;

  private textFilterSubject = new Subject<void>();
  private textFilterSubscription?: Subscription;

  constructor(
    private dataAcquisitionService: DataAcquisitionService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    // Debounce free-text typing so we don't fire a request per keystroke.
    this.textFilterSubscription = this.textFilterSubject
      .pipe(debounceTime(300))
      .subscribe(() => this.applyFilters());
    this.loadLocations();
  }

  ngOnDestroy(): void {
    this.textFilterSubscription?.unsubscribe();
  }

  loadLocations(): void {
    if (!this.facilityId) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    this.dataAcquisitionService.getLocationMappings(
      this.facilityId,
      this.paginationMetadata.pageNumber,
      this.paginationMetadata.pageSize,
      {
        locationId: this.locationIdFilter?.trim() || undefined,
        locationName: this.locationNameFilter?.trim() || undefined,
        locationAlias: this.locationAliasFilter?.trim() || undefined,
        partOfValue: this.partOfValueFilter?.trim() || undefined,
        isOrgLocation: this.toBool(this.isOrgLocationFilter),
        // Default to active-only; when "Show inactive" is checked, don't filter on active.
        isActive: this.showInactive ? undefined : true
      },
      this.currentSortBy,
      this.currentSortOrder
    ).subscribe({
      next: (result) => {
        this.dataSource.data = result.records ?? [];
        this.paginationMetadata = result.metadata;
        this.isLoading = false;
      },
      error: (err) => {
        this.dataSource.data = [];
        this.errorMessage = err?.message || 'Failed to load locations for this facility.';
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadLocations();
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction && this.sortFieldMap[sort.active]) {
      this.currentSortBy = this.sortFieldMap[sort.active];
      this.currentSortOrder = sort.direction === 'desc' ? 1 : 0;
    } else {
      // Cleared sort — fall back to the backend default.
      this.currentSortBy = undefined;
      this.currentSortOrder = undefined;
    }
    // Sorting changes ordering across the whole result set, so return to the first page.
    this.paginationMetadata.pageNumber = 0;
    this.loadLocations();
  }

  // Called by every free-text filter input; the shared debounce reloads once typing settles.
  onTextFilterChange(): void {
    this.textFilterSubject.next();
  }

  applyFilters(): void {
    // Any filter change resets to the first page so results aren't out of range.
    this.paginationMetadata.pageNumber = 0;
    this.loadLocations();
  }

  clearFilters(): void {
    this.locationIdFilter = '';
    this.locationNameFilter = '';
    this.locationAliasFilter = '';
    this.partOfValueFilter = '';
    this.isOrgLocationFilter = '';
    this.showInactive = false;
    this.applyFilters();
  }

  hasActiveFilters(): boolean {
    return !!this.locationIdFilter
      || !!this.locationNameFilter
      || !!this.locationAliasFilter
      || !!this.partOfValueFilter
      || this.isOrgLocationFilter !== ''
      || this.showInactive;
  }

  // Copies a value (e.g. the locationMappingId DB key) to the clipboard so it can be pasted
  // into a log search or DB query. Confirms via the shared snackbar.
  copyToClipboard(value: string | number, label: string = 'Value'): void {
    navigator.clipboard.writeText(String(value))
      .then(() => SnackbarHelper.showSuccessMessage(this.snackBar, `${label} copied to clipboard.`))
      .catch(() => SnackbarHelper.showErrorMessage(this.snackBar, 'Unable to copy to clipboard.'));
  }

  private toBool(value: TriStateFilter): boolean | undefined {
    return value === '' ? undefined : value === 'true';
  }
}
