import { MatTableDataSource, MatTableModule } from "@angular/material/table";
import { MatSort, MatSortModule } from "@angular/material/sort";

import { Component, OnInit, ViewChild } from '@angular/core';
import { SubPreQualReportCategoryModel } from "src/app/models/tenant/SubPreQualReportCategory.model";
import { CommonModule } from "@angular/common";
import { SubPreQualReportIssueModel } from "src/app/models/tenant/SubPreQualReportIssue.model";

@Component({
  selector: 'app-sub-pre-qual-report-issues',
  imports: [
    CommonModule,
    MatTableModule,
    MatSortModule
  ],
  templateUrl: './sub-pre-qual-report-issues.component.html',
  styleUrl: './sub-pre-qual-report-issues.component.scss'
})
export class SubPreQualReportIssuesComponent implements OnInit {
  acceptableIssues: SubPreQualReportCategoryModel[] = [];
  unacceptableIssues: SubPreQualReportCategoryModel[] = [];
  uncategorizedIssues: SubPreQualReportCategoryModel[] = [];

  acceptableIssuesDataSource!: MatTableDataSource<SubPreQualReportCategoryModel>;
  uncceptableIssuesDataSource!: MatTableDataSource<SubPreQualReportCategoryModel>;
  uncategorizedIssuesDataSource!: MatTableDataSource<SubPreQualReportCategoryModel>;

  expandedElement: SubPreQualReportCategoryModel | null = null;

  categoryColumns: string[] = ['name', 'quantity', 'guidance'];
  issueColumns: string[] = ['name', 'message', 'expression', 'location'];
  displayedColumnsWithExpansion: string[] = ['expanded'];

  isExpansionDetailRow = (index: number, row: SubPreQualReportCategoryModel) =>
    this.expandedElement === row;

  @ViewChild(MatSort, { static: true }) sort: MatSort = new MatSort;

  ngOnInit() {
    this.acceptableIssues = this.generateRandomReports(5);
    this.acceptableIssuesDataSource = new MatTableDataSource(this.acceptableIssues);
    this.acceptableIssuesDataSource.sort = this.sort;
  }

  private generateRandomReports(count: number): SubPreQualReportCategoryModel[] {
    const sampleNames = ['Sales Summary', 'Inventory Report', 'Customer Insights', 'Performance Metrics', 'Error Logs'];
    const sampleGuidance = [
      'Review weekly trends.',
      'Check stock levels regularly.',
      'Identify top customers.',
      'Evaluate KPIs monthly.',
      'Investigate recurring issues.'
    ];

    const issueTemplates: SubPreQualReportIssueModel[] = [
      { name: 'Missing Field', message: 'Name is required', expression: 'user.name', location: 'Row 1' },
      { name: 'Invalid Type', message: 'Quantity must be a number', expression: 'order.quantity', location: 'Row 2' }
    ];

    return Array.from({ length: count }, (_, i) => {
      const report = new SubPreQualReportCategoryModel();
      report.name = sampleNames[i % sampleNames.length];
      report.quantity = Math.floor(Math.random() * 100) + 1; // random number between 1–100
      report.guidance = sampleGuidance[i % sampleGuidance.length];
      report.issues = issueTemplates.slice(0, Math.floor(Math.random() * 3));
      return report;
    });
  }
}
