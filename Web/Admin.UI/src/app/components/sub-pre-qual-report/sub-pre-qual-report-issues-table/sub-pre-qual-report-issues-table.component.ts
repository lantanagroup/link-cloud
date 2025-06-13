import { MatTable, MatTableDataSource, MatTableModule } from "@angular/material/table";
import { MatSort, MatSortModule } from "@angular/material/sort";

import { ChangeDetectorRef, Component, ElementRef, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { CommonModule } from "@angular/common";
import { dummyCategories, dummyIssues } from "src/assets/dummy-data/sub-pre-qual-report-data";
import { animate, state, style, transition, trigger } from "@angular/animations";
import { Category, Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";

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
export class SubPreQualReportIssuesTableComponent {
  @ViewChild('sort', { static: true }) sort!: MatSort;

  dataSource: MatTableDataSource<Issue> = new MatTableDataSource<Issue>;
  issueColumns: string[] = ['name', 'message', 'expression', 'location'];

  constructor(
    private cd: ChangeDetectorRef,
    private el: ElementRef
  ) { }

  ngOnInit() {
    this.dataSource = new MatTableDataSource(dummyIssues);
    this.dataSource.sort = this.sort;
  }
}

// const ISSUES = dummyIssues;
