import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';

import { ReportingPlanService } from '../../../../services/gateway/dmrp/reporting-plan.service';
import { IFacilityReportingPlan } from '../../../../interfaces/dmrp/facility-reporting-plan.interface';
import { Frequency } from '../../../../interfaces/dmrp/measure-mapping.interface';

/**
 * Read-only view of what DMRP said the facility is enrolled to report, one row per measure per
 * reporting period. Rows with isReporting false are enrollments the facility has withdrawn from;
 * they are kept as history, so they are shown rather than hidden.
 *
 * The endpoint returns the facility's full history in one call (unpaged by design), so filtering
 * and pagination are client-side: filters narrow the in-memory list, the paginator pages it.
 */
@Component({
  selector: 'app-facility-reporting-plans',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatSortModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule
  ],
  templateUrl: './facility-reporting-plans.component.html',
  styleUrls: ['./facility-reporting-plans.component.scss']
})
export class FacilityReportingPlansComponent implements OnInit {
  @Input() facilityId: string = '';

  displayedColumns = ['measure', 'dqm', 'frequency', 'period', 'isReporting', 'lastUpdated'];
  dataSource = new MatTableDataSource<IFacilityReportingPlan>([]);

  /** Everything the service returned; dataSource holds the filtered/sorted view of it. */
  allPlans: IFacilityReportingPlan[] = [];
  /** Distinct periods present in the data, newest first, for the period filter. */
  periodOptions: { key: string; label: string }[] = [];
  /**
   * Distinct cadences present in the data, in canonical order. Derived from the rows rather
   * than MEASURE_MAPPING_FREQUENCIES so historical values a mapping no longer allows (Adhoc,
   * Discharge) remain filterable.
   */
  frequencyOptions: Frequency[] = [];

  filterText = '';
  filterPeriod: string | null = null;
  filterReporting: boolean | null = null;
  filterFrequency: Frequency | null = null;

  private static readonly frequencyOrder: readonly Frequency[] = [
    Frequency.Daily, Frequency.Weekly, Frequency.Monthly, Frequency.Adhoc, Frequency.Discharge
  ];

  readonly pageSizeOptions = [10, 25, 50];

  loading = false;
  loadFailed = false;

  private currentSort: Sort | null = null;

  private static readonly monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

  // The paginator lives inside the loaded branch of the template, so it appears after the
  // first render rather than at construction — hence a setter instead of a plain ViewChild.
  @ViewChild(MatPaginator) set paginator(paginator: MatPaginator | undefined) {
    if (paginator && this.dataSource.paginator !== paginator) {
      this.dataSource.paginator = paginator;
    }
  }

  constructor(private reportingPlanService: ReportingPlanService) {
  }

  ngOnInit(): void {
    this.loadReportingPlans();
  }

  loadReportingPlans(): void {
    this.loading = true;
    this.loadFailed = false;

    this.reportingPlanService.getReportingPlansForFacility(this.facilityId).subscribe({
      next: (plans) => {
        this.loading = false;
        this.allPlans = plans ?? [];
        this.periodOptions = this.buildPeriodOptions(this.allPlans);
        this.frequencyOptions = FacilityReportingPlansComponent.buildFrequencyOptions(this.allPlans);
        this.applyView();
      },
      error: () => {
        this.loading = false;
        this.loadFailed = true;
        this.allPlans = [];
        this.periodOptions = [];
        this.frequencyOptions = [];
        this.dataSource.data = [];
      }
    });
  }

  onFilterChange(): void {
    this.applyView();
    this.dataSource.paginator?.firstPage();
  }

  clearFilters(): void {
    this.filterText = '';
    this.filterPeriod = null;
    this.filterReporting = null;
    this.filterFrequency = null;
    this.onFilterChange();
  }

  hasActiveFilters(): boolean {
    return this.filterText.trim() !== '' || this.filterPeriod !== null
      || this.filterReporting !== null || this.filterFrequency !== null;
  }

  onSortChange(sort: Sort): void {
    this.currentSort = sort.active && sort.direction ? sort : null;
    this.applyView();
  }

  periodLabel(plan: IFacilityReportingPlan): string {
    return this.monthLabel(plan.reportingMonth, plan.reportingYear);
  }

  lastUpdated(plan: IFacilityReportingPlan): string {
    return plan.modifyDate ?? plan.createDate;
  }

  /** Recomputes the visible rows: filter, then the active sort (or the default ordering). */
  private applyView(): void {
    const filtered = this.allPlans.filter(plan => this.matchesFilters(plan));
    this.dataSource.data = this.currentSort
      ? this.sortBy(filtered, this.currentSort)
      : this.sortPlans(filtered);
  }

  private matchesFilters(plan: IFacilityReportingPlan): boolean {
    const text = this.filterText.trim().toLowerCase();
    const textMatches = text === ''
      || (plan.measure ?? '').toLowerCase().includes(text)
      || (plan.dqm ?? '').toLowerCase().includes(text);

    const periodMatches = this.filterPeriod === null
      || FacilityReportingPlansComponent.periodKey(plan) === this.filterPeriod;

    const reportingMatches = this.filterReporting === null
      || plan.isReporting === this.filterReporting;

    const frequencyMatches = this.filterFrequency === null
      || plan.frequency === this.filterFrequency;

    return textMatches && periodMatches && reportingMatches && frequencyMatches;
  }

  private static buildFrequencyOptions(plans: IFacilityReportingPlan[]): Frequency[] {
    const present = new Set(plans.map(p => p.frequency).filter((f): f is Frequency => f !== null));
    const ordered = FacilityReportingPlansComponent.frequencyOrder.filter(f => present.has(f));
    // Anything outside the canonical order (a future enum value) still gets an option.
    const unknown = [...present].filter(f => !FacilityReportingPlansComponent.frequencyOrder.includes(f));
    return [...ordered, ...unknown];
  }

  private buildPeriodOptions(plans: IFacilityReportingPlan[]): { key: string; label: string }[] {
    const byKey = new Map<string, IFacilityReportingPlan>();
    for (const plan of plans) {
      byKey.set(FacilityReportingPlansComponent.periodKey(plan), plan);
    }

    return [...byKey.values()]
      .sort((a, b) => this.comparePeriods(b, a))
      .map(plan => ({
        key: FacilityReportingPlansComponent.periodKey(plan),
        label: this.monthLabel(plan.reportingMonth, plan.reportingYear)
      }));
  }

  private monthLabel(month: number, year: number): string {
    const name = FacilityReportingPlansComponent.monthNames[month - 1];
    return name ? `${name} ${year}` : `${month}/${year}`;
  }

  private static periodKey(plan: IFacilityReportingPlan): string {
    return `${plan.reportingYear}-${plan.reportingMonth}`;
  }

  private sortBy(plans: IFacilityReportingPlan[], sort: Sort): IFacilityReportingPlan[] {
    const direction = sort.direction === 'asc' ? 1 : -1;

    return [...plans].sort((a, b) => {
      switch (sort.active) {
        case 'measure': return direction * (a.measure ?? '').localeCompare(b.measure ?? '');
        case 'dqm': return direction * (a.dqm ?? '').localeCompare(b.dqm ?? '');
        case 'frequency': return direction * (a.frequency ?? '').localeCompare(b.frequency ?? '');
        case 'period': return direction * this.comparePeriods(a, b);
        case 'isReporting': return direction * (Number(a.isReporting) - Number(b.isReporting));
        default: return 0;
      }
    });
  }

  /** Newest period first, then by measure, so the current month reads from the top. */
  private sortPlans(plans: IFacilityReportingPlan[]): IFacilityReportingPlan[] {
    return [...plans].sort((a, b) =>
      this.comparePeriods(b, a) || (a.measure ?? '').localeCompare(b.measure ?? ''));
  }

  private comparePeriods(a: IFacilityReportingPlan, b: IFacilityReportingPlan): number {
    return (a.reportingYear - b.reportingYear) || (a.reportingMonth - b.reportingMonth);
  }
}
