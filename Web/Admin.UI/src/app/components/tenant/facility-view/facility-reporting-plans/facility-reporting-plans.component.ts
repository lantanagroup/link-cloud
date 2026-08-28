import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ReportingPlanService } from '../../../../services/gateway/dmrp/reporting-plan.service';
import { IFacilityReportingPlan } from '../../../../interfaces/dmrp/facility-reporting-plan.interface';

/**
 * Read-only view of what DMRP said the facility is enrolled to report, one row per measure per
 * reporting period. Rows with isReporting false are enrollments the facility has withdrawn from;
 * they are kept as history, so they are shown rather than hidden.
 */
@Component({
  selector: 'app-facility-reporting-plans',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatSortModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './facility-reporting-plans.component.html',
  styleUrls: ['./facility-reporting-plans.component.scss']
})
export class FacilityReportingPlansComponent implements OnInit {
  @Input() facilityId: string = '';

  displayedColumns = ['measure', 'dqm', 'frequency', 'period', 'isReporting', 'lastUpdated'];
  dataSource = new MatTableDataSource<IFacilityReportingPlan>([]);

  loading = false;
  loadFailed = false;

  private static readonly monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

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
        this.dataSource.data = this.sortPlans(plans ?? []);
      },
      error: () => {
        this.loading = false;
        this.loadFailed = true;
        this.dataSource.data = [];
      }
    });
  }

  onSortChange(sort: Sort): void {
    const data = [...this.dataSource.data];

    if (!sort.active || !sort.direction) {
      this.dataSource.data = this.sortPlans(data);
      return;
    }

    const direction = sort.direction === 'asc' ? 1 : -1;

    this.dataSource.data = data.sort((a, b) => {
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

  periodLabel(plan: IFacilityReportingPlan): string {
    const name = FacilityReportingPlansComponent.monthNames[plan.reportingMonth - 1];
    return name ? `${name} ${plan.reportingYear}` : `${plan.reportingMonth}/${plan.reportingYear}`;
  }

  lastUpdated(plan: IFacilityReportingPlan): string {
    return plan.modifyDate ?? plan.createDate;
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
