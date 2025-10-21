import { Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";
import { Component, OnDestroy, OnInit, ViewChild, Input, OnChanges, SimpleChanges } from '@angular/core';
import { MatSort, MatSortModule } from "@angular/material/sort";
import { MatTableDataSource, MatTableModule } from "@angular/material/table";
import { MatPaginator, MatPaginatorModule } from "@angular/material/paginator";

import { ActivatedRoute } from '@angular/router';

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
    MatTableModule,
    MatSortModule,
    MatPaginatorModule
  ],
  templateUrl: './sub-pre-qual-report-issues-table.component.html',
  styleUrls: ['./sub-pre-qual-report-issues-table.component.scss'],
  standalone: true
})
export class SubPreQualReportIssuesTableComponent implements OnInit, OnDestroy, OnChanges {
  @ViewChild('sort', { static: true }) sort!: MatSort;
  @ViewChild(MatPaginator, { static: true }) paginator!: MatPaginator;
  @Input() reportIssues: IValidationIssue[] | undefined;

  // Main data source for the issues table
  dataSource: MatTableDataSource<Issue> = new MatTableDataSource<Issue>();

  // Column definitions for the table
  issueColumns: string[] = ['message', 'expression', 'location'];

  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';
  category: string = '';

  constructor(
    private route: ActivatedRoute,
  ) { }

  ngOnInit() {
    // Subscribe to route parameters to get facilityId and submissionId
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
      this.category = params['categoryId'];
      this.loadReportData();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Only reload if reportIssues or reportIssuesSummary inputs have changed
    if (changes['reportIssues']) {
      this.loadReportData();
    }
  }

  /**
   * Loads report data from the API
   * Gets all issues and filters for uncategorized ones
   * Transforms the data into the format expected by the table
   */
  private loadReportData(): void {
    // Transform issues into the format expected by the table
    if (!this.reportIssues || !this.category) return;

    var filteredIssues = this.reportIssues;

    if (this.category === 'Uncategorized') {
      filteredIssues = filteredIssues.filter(issue => !issue.categories || issue.categories.length === 0);
    } else {
      filteredIssues = filteredIssues.filter(issue => (issue.categories ?? []).some(s => s.id === this.category));
    }
    var transformedIssues = filteredIssues.map(issue => ({
      name: issue.code,
      message: issue.message,
      expression: issue.expression,
      location: issue.location
    }));

    this.dataSource = new MatTableDataSource(transformedIssues);
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
