import { MatTable, MatTableDataSource, MatTableModule } from "@angular/material/table";
import { MatSort, MatSortModule } from "@angular/material/sort";

import { ChangeDetectorRef, Component, ElementRef, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { CommonModule } from "@angular/common";
import { dummyCategories } from "src/assets/dummy-data/sub-pre-qual-report-data";
import { animate, state, style, transition, trigger } from "@angular/animations";
import { Category, Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";

@Component({
  selector: 'app-sub-pre-qual-report-categories-table',
  imports: [
    CommonModule,
    MatTableModule,
    MatSortModule
  ],
  templateUrl: './sub-pre-qual-report-categories-table.component.html',
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: 'unset' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
  styleUrl: './sub-pre-qual-report-categories-table.component.scss'
})
export class SubPreQualReportCategoriesTableComponent {
  @ViewChild('outerSort', { static: true }) sort: MatSort = new MatSort;
  @ViewChildren('innerSort') innerSort: QueryList<MatSort> = new QueryList;
  @ViewChildren('innerTables') innerTables: QueryList<MatTable<Issue>> = new QueryList;

  dataSource: MatTableDataSource<Category> = new MatTableDataSource<Category>;
  categoriesData: Category[] = [];
  categoryColumns: string[] = ['name', 'quantity', 'guidance'];
  issueColumns: string[] = ['name', 'message', 'expression', 'location'];
  expandedCategory: Category | null = null;

  constructor(
    private cd: ChangeDetectorRef,
    private el: ElementRef
  ) { }

  ngOnInit() {
    CATEGORIES.forEach(category => {
      if (category.issues && Array.isArray(category.issues) && category.issues.length) {
        this.categoriesData = [...this.categoriesData, { ...category, issues: new MatTableDataSource(category.issues) }];
      } else {
        this.categoriesData = [...this.categoriesData, category];
      }
    });
    this.dataSource = new MatTableDataSource(this.categoriesData);
    this.dataSource.sort = this.sort;
  }

  toggleRow(category: Category) {
    category.issues && (category.issues as MatTableDataSource<Issue>).data.length ? (this.expandedCategory = this.expandedCategory === category ? null : category) : null;
    this.cd.detectChanges();
    this.innerTables.forEach((table, index) => (table.dataSource as MatTableDataSource<Issue>).sort = this.innerSort.toArray()[index]);
  }
}

const CATEGORIES = dummyCategories;
