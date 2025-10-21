import {Component, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatIconModule} from '@angular/material/icon';
import {MatButtonModule} from '@angular/material/button';
import {MatDialogModule} from '@angular/material/dialog';
import {MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTableModule} from '@angular/material/table';
import {MatPaginator, MatPaginatorModule} from '@angular/material/paginator';
import {MatSortModule} from '@angular/material/sort';
import {MatTooltipModule} from '@angular/material/tooltip';
import {BehaviorSubject, combineLatest, Observable, of} from 'rxjs';
import {debounceTime, distinctUntilChanged, map, startWith, switchMap, tap} from 'rxjs/operators';
import {CommonModule} from '@angular/common';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {FormControl, FormsModule, ReactiveFormsModule} from "@angular/forms";
import {MatCheckbox} from "@angular/material/checkbox";
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatAutocompleteModule} from '@angular/material/autocomplete';
import {ReportScheduleGridBase} from '../report-schedule-grid.base';

@Component({
  selector: 'app-reports-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatPaginatorModule,
    MatTableModule,
    MatSortModule,
    MatTooltipModule,
    RouterLink,
    FontAwesomeModule,
    FormsModule,
    ReactiveFormsModule,
    MatCheckbox,
    MatSnackBarModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatAutocompleteModule
  ],
  templateUrl: './reports-dashboard.component.html',
  styleUrls: ['./reports-dashboard.component.scss']
})
export class ReportsDashboardComponent extends ReportScheduleGridBase implements OnInit, OnDestroy {
  @ViewChild(MatPaginator, { static: false }) paginator!: MatPaginator;

  // Facility is a filter here rather than fixed, so the dashboard owns the autocomplete.
  facilityInputControl = new FormControl<string>('');
  selectedFacilityId: string | null = null;
  filteredFacilities: Observable<{ facilityId: string; facilityName: string }[]> = of([]);
  private showDeletedSubject = new BehaviorSubject<boolean>(false);

  protected override pageSizeStorageKey = 'reportsDashboardPageSize';

  protected get facilityFilter(): string | undefined {
    return this.selectedFacilityId || this.facilityInputControl.value || undefined;
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router) {
    super();
  }

  ngOnInit(): void {
    this.initPagination();

    this.filteredFacilities = combineLatest([
      this.facilityInputControl.valueChanges.pipe(
        startWith(''),
        debounceTime(300),
        distinctUntilChanged(),
        // Clear the selected ID whenever the user edits the text. emitEvent:false
        // (used in onFacilitySelected) bypasses valueChanges, so the tap only
        // fires on real keystrokes, not on programmatic selection.
        tap(() => { this.selectedFacilityId = null; })
      ),
      this.showDeletedSubject
    ]).pipe(
      switchMap(([term, includeDeleted]) => {
        const search = typeof term === 'string' ? term : '';
        return this.tenantService.autocompleteFacilities(search, includeDeleted);
      }),
      map(results => Object.entries(results || {}).map(([facilityId, facilityName]) => ({ facilityId, facilityName: facilityName as string })))
    );

    this.watchReportIdFilter();
    this.loadReportSchedules();
  }

  override clearFilters(): void {
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.selectedFacilityId = null;
    super.clearFilters();
  }

  override hasActiveFilters(): boolean {
    return !!(this.selectedFacilityId || this.facilityInputControl.value) || super.hasActiveFilters();
  }

  // The autocomplete list respects the deleted toggle, so it has to be told about it too.
  override onShowDeletedChange(): void {
    this.showDeletedSubject.next(this.showDeleted);
    super.onShowDeletedChange();
  }

  onFacilitySelected(fac: { facilityId: string; facilityName: string }): void {
    this.selectedFacilityId = fac.facilityId;
    this.facilityInputControl.setValue(fac.facilityName || fac.facilityId, { emitEvent: false });
    this.applyFilters();
  }

  clearFacilityFilter(): void {
    this.selectedFacilityId = null;
    this.facilityInputControl.setValue('', { emitEvent: false });
    this.applyFilters();
  }

  displayFacility(fac: { facilityId: string; facilityName: string } | string | null): string {
    if (!fac) return '';
    if (typeof fac === 'string') return fac;
    return fac.facilityName || fac.facilityId;
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }
}
