import { DatePipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, Subscription } from 'rxjs';

import {
  IRubricSettings,
  IRubricSummary,
  IRubricVersionSummary,
  RubricVersionStatus
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { newestInStatus, pipelineUse, pipelineUseTitle, RubricPipelineUse } from '../rubric-display';

/** A rubric as the list shows it: the newest version in each state, worked out once per page load. */
interface RubricRow {
  rubric: IRubricSummary;
  published?: IRubricVersionSummary;
  draft?: IRubricVersionSummary;
  retired?: IRubricVersionSummary;
  /** How the reporting pipeline uses this rubric; null when it does not. */
  pipelineUse: RubricPipelineUse | null;
}

/** How long typing in the search box has to pause before the list is asked for again. */
export const SEARCH_DEBOUNCE_MS = 300;

/**
 * The rubrics the Validation service knows, one row each. The service searches, filters, sorts and
 * pages; this screen only asks.
 */
@Component({
  selector: 'app-rubric-list',
  imports: [
    DatePipe,
    FormsModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSortModule,
    MatTableModule,
    RouterLink
  ],
  templateUrl: './rubric-list.component.html',
  styleUrls: ['./rubric-list.component.scss']
})
export class RubricListComponent implements OnInit, OnDestroy {
  readonly columns = ['rubricId', 'owner', 'published', 'draft', 'updatedAt'];
  readonly pageSizeOptions = [10, 20, 50];
  readonly pipelineUseTitle = pipelineUseTitle;
  readonly statusOptions: { value: RubricVersionStatus | ''; label: string }[] = [
    { value: '', label: 'Any' },
    { value: 'DRAFT', label: 'Has a draft' },
    { value: 'PUBLISHED', label: 'Has a published version' },
    { value: 'RETIRED', label: 'Has a retired version' }
  ];

  rows: RubricRow[] = [];
  totalCount = 0;
  pageIndex = 0;
  pageSize = 20;
  sortBy = 'rubricId';
  sortDirection: 'asc' | 'desc' = 'asc';

  search = '';
  status: RubricVersionStatus | '' = '';
  owner = '';
  /** The owners to offer. Empty until they are loaded, and if they could not be: then there is no owner filter. */
  owners: string[] = [];

  loading = false;
  /** Why the list could not be loaded, when it could not. */
  error: string | null = null;

  private settings: IRubricSettings | null = null;
  private rubrics: IRubricSummary[] = [];
  private request?: Subscription;
  private readonly searchTyped = new Subject<string>();
  private readonly subscriptions = new Subscription();

  constructor(private rubricService: RubricService, private changeDetector: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    this.subscriptions.add(
      this.searchTyped.pipe(debounceTime(SEARCH_DEBOUNCE_MS), distinctUntilChanged()).subscribe(() => this.applyFilters()));

    // Neither is needed to show the list. Without the owners there is no owner filter; without the
    // settings no rubric is marked as in use.
    this.subscriptions.add(
      this.rubricService.getOwners().pipe(catchError(() => of([] as string[]))).subscribe(owners => {
        this.owners = owners;
        this.changeDetector.markForCheck();
      }));
    this.subscriptions.add(
      this.rubricService.getSettings().subscribe(settings => {
        this.settings = settings;
        this.buildRows();
        this.changeDetector.markForCheck();
      }));

    this.load();
  }

  ngOnDestroy(): void {
    this.request?.unsubscribe();
    this.subscriptions.unsubscribe();
  }

  get filtered(): boolean {
    return !!(this.search.trim() || this.status || this.owner);
  }

  /** True once a load has answered with no rubrics at all. */
  get empty(): boolean {
    return !this.loading && !this.error && this.rows.length === 0;
  }

  onSearchTyped(): void {
    this.searchTyped.next(this.search.trim());
  }

  /** A filter changed: the page being looked at no longer means anything, so go back to the first. */
  applyFilters(): void {
    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.status = '';
    this.owner = '';
    this.applyFilters();
  }

  onSort(sort: Sort): void {
    // Material clears the direction on the third click; that means "back to the default order".
    this.sortBy = sort.direction ? sort.active : 'rubricId';
    this.sortDirection = sort.direction || 'asc';
    this.pageIndex = 0;
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  load(): void {
    // A newer request replaces one still in flight, so an old answer cannot overwrite a new one.
    this.request?.unsubscribe();
    this.loading = true;
    this.error = null;

    this.request = this.rubricService.listRubrics({
      search: this.search.trim() || undefined,
      status: this.status || undefined,
      owner: this.owner || undefined,
      // Every version of the rubrics a status matched, so the list still shows what is published for a
      // rubric that was found by its draft.
      allVersions: true,
      page: this.pageIndex,
      size: this.pageSize,
      sortBy: this.sortBy,
      sortDirection: this.sortDirection
    }).subscribe({
      next: page => {
        this.rubrics = page.content;
        this.buildRows();
        this.totalCount = page.totalElements;
        this.pageIndex = page.page;
        this.loading = false;
        // The answer arrives outside anything Angular watches for; say the page has changed.
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.rubrics = [];
        this.rows = [];
        this.totalCount = 0;
        this.error = error.message;
        this.loading = false;
        this.changeDetector.markForCheck();
      }
    });
  }

  private buildRows(): void {
    this.rows = this.rubrics.map(rubric => ({
      rubric,
      published: newestInStatus(rubric.versions, 'PUBLISHED'),
      draft: newestInStatus(rubric.versions, 'DRAFT'),
      retired: newestInStatus(rubric.versions, 'RETIRED'),
      pipelineUse: pipelineUse(this.settings, rubric.rubricId)
    }));
  }
}
