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
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatDialog, MatDialogModule} from '@angular/material/dialog';
import {MatTooltipModule} from '@angular/material/tooltip';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {Subject, Subscription} from 'rxjs';
import {debounceTime} from 'rxjs/operators';
import {DataAcquisitionService} from '../../../../services/gateway/data-acquisition/data-acquisition.service';
import {SnackbarHelper} from '../../../../services/snackbar-helper';
import {PaginationMetadata} from '../../../../models/pagination-metadata.model';
import {IEncounterLocationModel, IEncounterMappingModel} from '../../../../interfaces/data-acquisition/encounter-mapping-model.interface';
import {LocationDetailsDialogComponent} from '../location-details-dialog/location-details-dialog.component';

// A tri-state filter value: '' means "Any" (no filter), otherwise a stringified boolean.
type TriStateFilter = '' | 'true' | 'false';

@Component({
  selector: 'app-encounters-list',
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
    MatProgressSpinnerModule,
    MatDialogModule,
    MatTooltipModule,
    MatSnackBarModule
  ],
  templateUrl: './encounters-list.component.html',
  styleUrl: './encounters-list.component.scss'
})
export class EncountersListComponent implements OnInit, OnDestroy {

  @Input() facilityId: string = '';

  // encounterMappingId (the DB primary key) is surfaced first as a copyable debugging aid so a
  // row can be cross-referenced against backend logs; modifiedDate is shown last for "when did
  // this change" investigations.
  displayedColumns = ['encounterMappingId', 'encounterId', 'patientId', 'locationId', 'mappedToOrg', 'createDate', 'modifiedDate'];
  dataSource = new MatTableDataSource<IEncounterMappingModel>([]);
  paginationMetadata: PaginationMetadata = Object.assign(new PaginationMetadata(), {
    pageSize: 10,
    pageNumber: 0,
    totalCount: 0,
    totalPages: 0
  });

  // Free-text (exact match) filters.
  encounterIdFilter = '';
  patientIdFilter = '';

  // Tri-state "Mapped To Org" filter: '' = any, 'true' = mapped, 'false' = not mapped.
  mappedToOrgFilter: TriStateFilter = '';

  // Undefined => let the backend apply its default sort (CreateDate desc).
  // sortOrder follows the API convention: 0 = Ascending, 1 = Descending.
  currentSortBy?: string;
  currentSortOrder?: number;

  // Maps sortable UI column names to the API field names the backend allows.
  private readonly sortFieldMap: { [key: string]: string } = {
    encounterId: 'EncounterId',
    patientId: 'PatientId'
  };

  isLoading = false;
  errorMessage: string | null = null;

  private textFilterSubject = new Subject<void>();
  private textFilterSubscription?: Subscription;

  constructor(
    private dataAcquisitionService: DataAcquisitionService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    // Debounce free-text typing so we don't fire a request per keystroke.
    this.textFilterSubscription = this.textFilterSubject
      .pipe(debounceTime(300))
      .subscribe(() => this.applyFilters());
    this.loadEncounters();
  }

  ngOnDestroy(): void {
    this.textFilterSubscription?.unsubscribe();
  }

  loadEncounters(): void {
    if (!this.facilityId) {
      return;
    }

    this.isLoading = true;
    this.errorMessage = null;

    this.dataAcquisitionService.getEncounterMappings(
      this.facilityId,
      this.paginationMetadata.pageNumber,
      this.paginationMetadata.pageSize,
      {
        encounterId: this.encounterIdFilter?.trim() || undefined,
        patientId: this.patientIdFilter?.trim() || undefined,
        mappedToOrg: this.toBool(this.mappedToOrgFilter)
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
        this.errorMessage = err?.message || 'Failed to load encounters for this facility.';
        this.isLoading = false;
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.paginationMetadata.pageSize = event.pageSize;
    this.paginationMetadata.pageNumber = event.pageIndex;
    this.loadEncounters();
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
    this.loadEncounters();
  }

  // Called by every free-text filter input; the shared debounce reloads once typing settles.
  onTextFilterChange(): void {
    this.textFilterSubject.next();
  }

  applyFilters(): void {
    // Any filter change resets to the first page so results aren't out of range.
    this.paginationMetadata.pageNumber = 0;
    this.loadEncounters();
  }

  clearFilters(): void {
    this.encounterIdFilter = '';
    this.patientIdFilter = '';
    this.mappedToOrgFilter = '';
    this.applyFilters();
  }

  hasActiveFilters(): boolean {
    return !!this.encounterIdFilter || !!this.patientIdFilter || !!this.mappedToOrgFilter;
  }

  // LocationId is read-only and comma-joined when an encounter maps to multiple locations.
  getLocationIds(element: IEncounterMappingModel): string {
    return (element.encounterLocations ?? [])
      .map(l => l.locationId)
      .filter((id): id is string => !!id)
      .join(', ');
  }

  // The locations that have a resolvable LocationId — rendered as individual detail links.
  getLocations(element: IEncounterMappingModel): IEncounterLocationModel[] {
    return (element.encounterLocations ?? []).filter(l => !!l.locationId);
  }

  // Copies a value (e.g. the encounterMappingId DB key) to the clipboard so it can be pasted
  // into a log search or DB query. Confirms via the shared snackbar.
  copyToClipboard(value: string | number, label: string = 'Value'): void {
    navigator.clipboard.writeText(String(value))
      .then(() => SnackbarHelper.showSuccessMessage(this.snackBar, `${label} copied to clipboard.`))
      .catch(() => SnackbarHelper.showErrorMessage(this.snackBar, 'Unable to copy to clipboard.'));
  }

  // A best-effort debugging hint explaining why an encounter did not map to the organization.
  // Derived only from the data on the row, so it points at the likely cause rather than asserting
  // it — open the location details to confirm (e.g. an inactive underlying mapping).
  getUnmappedReason(element: IEncounterMappingModel): string | null {
    if (element.mappedToOrg) {
      return null;
    }
    const locations = element.encounterLocations ?? [];
    if (locations.length === 0) {
      return 'Encounter has no locations to map.';
    }
    const resolvable = locations.filter(l => !!l.locationId);
    if (resolvable.length === 0) {
      return 'Encounter references location(s) with no matching active organization mapping.';
    }
    return 'Encounter references location(s) that are not mapped as organization locations. Open a location for details.';
  }

  // Opens the read-only location-details dialog for the clicked location.
  openLocationDetails(location: IEncounterLocationModel): void {
    this.dialog.open(LocationDetailsDialogComponent, {
      data: { locationMappingId: location.organizationLocationMappingId },
      width: '480px'
    });
  }

  private toBool(value: TriStateFilter): boolean | undefined {
    return value === '' ? undefined : value === 'true';
  }
}
