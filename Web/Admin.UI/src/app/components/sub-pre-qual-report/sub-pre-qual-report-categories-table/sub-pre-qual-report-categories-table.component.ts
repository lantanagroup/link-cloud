import { Category, Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";
import { Component, Input, OnInit, QueryList, ViewChild, ViewChildren, OnChanges, SimpleChanges } from '@angular/core';
import { IValidationIssue, IValidationIssueCategorySummary } from '../../tenant/facility-view/report-view.interface';
import { MatSort, MatSortModule } from "@angular/material/sort";
import { MatTable, MatTableDataSource, MatTableModule } from "@angular/material/table";
import { Subscription } from 'rxjs';
import { animate, state, style, transition, trigger } from "@angular/animations";
import { ActivatedRoute, RouterModule } from '@angular/router';
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";

/**
 * Interface that extends Category to include a MatTableDataSource for issues
 * This allows us to have sortable and filterable tables for each category's issues
 */
interface CategoryWithDataSource extends Category {
  issues: MatTableDataSource<Issue>;
}

/**
 * Interface for the raw category data before it's transformed into a CategoryWithDataSource
 */
interface CategoryData {
  id: string;
  name: string;
  quantity: number;
  guidance: string;
  issues: Issue[];
}

/**
 * Component that displays issues grouped by their categories
 * Shows both acceptable and unacceptable issues in an expandable table format
 * Each category row can be expanded to show the individual issues within that category
 */
@Component({
  selector: 'app-sub-pre-qual-report-categories-table',
  imports: [
    MatTableModule,
    MatSortModule,
    VdIconComponent,
    RouterModule
  ],
  templateUrl: './sub-pre-qual-report-categories-table.component.html',
  animations: [
    trigger('detailExpand', [
      state('collapsed', style({ height: '0px', minHeight: '0' })),
      state('expanded', style({ height: 'unset' })),
      transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
    ]),
  ],
  styleUrls: ['./sub-pre-qual-report-categories-table.component.scss'],
  standalone: true
})
export class SubPreQualReportCategoriesTableComponent implements OnInit, OnChanges {
  @Input() showAcceptable: boolean = false;
  @Input() reportIssues: IValidationIssue[] | undefined;
  @Input() reportIssuesSummary: IValidationIssueCategorySummary[] | undefined;

  @ViewChild('outerSort', { static: true }) sort!: MatSort;
  @ViewChildren('innerSort') innerSort: QueryList<MatSort> = new QueryList;
  @ViewChildren('innerTables') innerTables: QueryList<MatTable<Issue>> = new QueryList;

  // Main data source for the categories table
  dataSource: MatTableDataSource<CategoryWithDataSource> = new MatTableDataSource<CategoryWithDataSource>();
  categoriesData: CategoryWithDataSource[] = [];

  // Column definitions for the table
  categoryColumns = [
    { header: 'Category', key: 'name' },
    { header: 'Number of Issues', key: 'quantity' },
    { header: 'Guidance', key: 'guidance' },
  ];
  categoryColumnKeys = this.categoryColumns.map(col => col.key);

  // Track which category is currently expanded
  expandedCategory: CategoryWithDataSource | null = null;

  private subscription: Subscription | undefined;
  facilityId: string = '';
  submissionId: string = '';

  constructor(
    private route: ActivatedRoute
  ) { }

  ngOnInit() {
    // Subscribe to route parameters to get facilityId and submissionId
    this.subscription = this.route.params.subscribe(params => {
      this.facilityId = params['facilityId'];
      this.submissionId = params['submissionId'];
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Only reload if reportIssues or reportIssuesSummary inputs have changed
    if (changes['reportIssues'] || changes['reportIssuesSummary']) {
      this.loadReportData();
    }
  }

  /**
   * Loads report data from the API
   * First gets all issues, then gets their summary
   * Transforms the data into categories with their respective issues
   */
  private loadReportData(): void {
    // Transform the data into categories
    const categories = this.transformDataToCategories(this.reportIssuesSummary ?? [], this.reportIssues ?? []);

    this.categoriesData = categories.map(category => ({
      ...category,
      issues: new MatTableDataSource(category.issues)
    }));

    this.dataSource = new MatTableDataSource(this.categoriesData);
    this.dataSource.sort = this.sort;
  }

  /**
   * Transforms the API response into categories with their issues
   * Groups issues by their categories and includes category metadata
   * Filters out uncategorized issues and filters by acceptable status
   */
  private transformDataToCategories(summary: IValidationIssueCategorySummary[], issues: IValidationIssue[]): CategoryData[] {
  return summary
    //.filter(summaryItem => summaryItem.value !== 'Uncategorized') // Remove uncategorized category
    .map(summaryItem => {
      if (summaryItem.value === 'Uncategorized') {
        return {
          id: 'Uncategorized',
          name: summaryItem.value,
          quantity: issues.filter(issue => issue.categories.length === 0 && !this.showAcceptable).length,
          guidance: 'These issues are not categorized and must be reviewed individually.',
          issues: []
        };
      }
      else {
        // Find all issues that belong to this category
        const categoryIssues = issues.filter(issue =>
          issue.categories.some(cat =>
            cat.title === summaryItem.value &&
            cat.acceptable === this.showAcceptable
          ) || (!issue.categories && summaryItem.value === 'Uncategorized')
        );

        // Get the first category that matches to get guidance
        const firstMatchingCategory = categoryIssues[0]?.categories.find(cat =>
          cat.title === summaryItem.value &&
          cat.acceptable === this.showAcceptable
        );

        // Transform issues into the format expected by the table
        const categoryIssuesList = categoryIssues.map(issue => ({
          id: issue.id,
          name: issue.code,
          message: issue.message,
          expression: issue.expression,
          location: issue.location
        }));

        return {
          id: firstMatchingCategory?.id ?? '',
          name: summaryItem.value,
          quantity: categoryIssuesList.length,
          guidance: firstMatchingCategory?.guidance || '',
          issues: categoryIssuesList
        };
      }
    })
    .filter(category => category.quantity > 0); // Remove categories with no issues
}

ngOnDestroy(): void {
  if(this.subscription) {
  this.subscription.unsubscribe();
}
  }
}
