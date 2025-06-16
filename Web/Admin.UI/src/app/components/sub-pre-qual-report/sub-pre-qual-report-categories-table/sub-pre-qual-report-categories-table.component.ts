import { MatTable, MatTableDataSource, MatTableModule } from "@angular/material/table";
import { MatSort, MatSortModule } from "@angular/material/sort";

import { ChangeDetectorRef, Component, ElementRef, Input, OnInit, QueryList, ViewChild, ViewChildren } from '@angular/core';
import { CommonModule } from "@angular/common";
import { animate, state, style, transition, trigger } from "@angular/animations";
import { Category, Issue } from "src/app/interfaces/sub-pre-qual-report-models.interface";
import { VdIconComponent } from "../../core/vd-icon/vd-icon.component";
import { ActivatedRoute } from '@angular/router';
import { FacilityViewService } from '../../tenant/facility-view/facility-view.service';
import { IReportIssue, IReportIssueCategorySummary } from '../../tenant/facility-view/report-view.interface';
import { Subscription } from 'rxjs';

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
    CommonModule,
    MatTableModule,
    MatSortModule,
    VdIconComponent
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
export class SubPreQualReportCategoriesTableComponent implements OnInit {
  @Input() showAcceptable: boolean = false;

  @ViewChild('outerSort', { static: true }) sort: MatSort = new MatSort;
  @ViewChildren('innerSort') innerSort: QueryList<MatSort> = new QueryList;
  @ViewChildren('innerTables') innerTables: QueryList<MatTable<Issue>> = new QueryList;

  // Main data source for the categories table
  dataSource: MatTableDataSource<CategoryWithDataSource> = new MatTableDataSource<CategoryWithDataSource>();
  categoriesData: CategoryWithDataSource[] = [];
  
  // Column definitions for the tables
  categoryColumns: string[] = ['name', 'quantity', 'guidance'];
  issueColumns: string[] = ['name', 'message', 'expression', 'location'];
  
  // Track which category is currently expanded
  expandedCategory: CategoryWithDataSource | null = null;

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
   * First gets all issues, then gets their summary
   * Transforms the data into categories with their respective issues
   */
  private loadReportData(): void {
    this.facilityViewService.getReportIssues(this.facilityId, this.submissionId).subscribe({
      next: (issues: IReportIssue[]) => {
        // Get the summary data
        this.facilityViewService.getReportIssuesSummary(issues).subscribe({
          next: (summary: IReportIssueCategorySummary[]) => {
            // Transform the data into categories
            const categories = this.transformDataToCategories(summary, issues);
            
            // Update the table data with MatTableDataSource for each category's issues
            this.categoriesData = categories.map(category => ({
              ...category,
              issues: new MatTableDataSource(category.issues)
            }));
            
            this.dataSource = new MatTableDataSource(this.categoriesData);
            this.dataSource.sort = this.sort;
          },
          error: (error) => {
            console.error('Error getting report issues summary:', error);
          }
        });
      },
      error: (error) => {
        console.error('Error getting report issues:', error);
      }
    });
  }

  /**
   * Transforms the API response into categories with their issues
   * Groups issues by their categories and includes category metadata
   * Filters out uncategorized issues and filters by acceptable status
   */
  private transformDataToCategories(summary: IReportIssueCategorySummary[], issues: IReportIssue[]): CategoryData[] {
    return summary
      .filter(summaryItem => summaryItem.value !== 'Uncategorized') // Remove uncategorized category
      .map(summaryItem => {
        // Find all issues that belong to this category
        const categoryIssues = issues.filter(issue => 
          issue.categories.some(cat => 
            cat.title === summaryItem.value && 
            cat.acceptable === this.showAcceptable
          )
        );

        // Get the first category that matches to get guidance
        const firstMatchingCategory = categoryIssues[0]?.categories.find(cat => 
          cat.title === summaryItem.value && 
          cat.acceptable === this.showAcceptable
        );

        // Transform issues into the format expected by the table
        const categoryIssuesList = categoryIssues.map(issue => ({
          name: issue.code,
          message: issue.message,
          expression: issue.expression,
          location: issue.location
        }));

        return {
          name: summaryItem.value,
          quantity: categoryIssuesList.length,
          guidance: firstMatchingCategory?.guidance || '',
          issues: categoryIssuesList
        };
      })
      .filter(category => category.issues.length > 0); // Remove categories with no issues
  }

  /**
   * Toggles the expansion of a category row
   * When expanded, shows the issues table for that category
   */
  toggleRow(category: CategoryWithDataSource) {
    if (category.issues.data.length) {
      this.expandedCategory = this.expandedCategory === category ? null : category;
      this.cd.detectChanges();
      this.innerTables.forEach((table, index) => {
        const dataSource = table.dataSource as MatTableDataSource<Issue>;
        dataSource.sort = this.innerSort.toArray()[index];
      });
    }
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
