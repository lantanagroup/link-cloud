import { Category, Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";
import { ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { MatSort, MatSortModule } from "@angular/material/sort";
import { MatTable, MatTableDataSource, MatTableModule } from "@angular/material/table";
import { animate, state, style, transition, trigger } from "@angular/animations";

import { ActivatedRoute } from '@angular/router';
import { CommonModule } from "@angular/common";
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IValidationIssue } from '../../tenant/facility-view/report-view.interface';
import { Subscription } from 'rxjs';

/**
 * Component that displays uncategorized issues in a table format
 * Shows issues that have no categories or only have the "Uncategorized" category
 * This is separate from the categories table which shows acceptable and unacceptable issues
 */
@Component({
  selector: 'app-sub-pre-qual-report-issues-table',
  imports: [
    CommonModule,
    MatTableModule,
    MatSortModule
  ],
  templateUrl: './sub-pre-qual-report-issues-table.component.html',
  styleUrls: ['./sub-pre-qual-report-issues-table.component.scss'],
  standalone: true
})
export class SubPreQualReportIssuesTableComponent implements OnInit, OnDestroy {
  @ViewChild('sort', { static: true }) sort!: MatSort;

  // Main data source for the issues table
  dataSource: MatTableDataSource<Issue> = new MatTableDataSource<Issue>();

  // Column definitions for the table
  issueColumns: string[] = ['name', 'message', 'expression', 'location'];

  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';

  constructor(
    private cd: ChangeDetectorRef,
    private el: ElementRef,
    private route: ActivatedRoute,
    private facilityViewService: FacilityViewService
  ) { }

  ngOnInit() {
    // Subscribe to route parameters to get facilityId and submissionId
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
      this.loadReportData();
    });
  }

  /**
   * Loads report data from the API
   * Gets all issues and filters for uncategorized ones
   * Transforms the data into the format expected by the table
   */
  private loadReportData(): void {
    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (issues: IValidationIssue[]) => {
        // Transform issues into the format expected by the table
        const transformedIssues = issues
        .filter(issue => issue.categories == null || (Array.isArray(issue.categories) && issue.categories.length === 0))
        .map(issue => ({
          name: issue.code,
          message: issue.message,
          expression: issue.expression,
          location: issue.location
        }));

        this.dataSource = new MatTableDataSource(transformedIssues);
        this.dataSource.sort = this.sort;
      },
      error: (error) => {
        console.error('Error getting report issues:', error);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}

// const ISSUES = dummyIssues;
