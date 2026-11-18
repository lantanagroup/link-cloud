import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, forkJoin, map, Observable, of, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricDetail,
  IRubricVersionDetail,
  IRubricVersionSummary
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { compareDefinitions, RubricComparison } from '../rubric-compare';
import { checkSummary, checkTypeLabel, statusLabel } from '../rubric-display';

/** The two versions to compare, when the address does not say: the newest against the active one, or the one before it. */
export function defaultComparison(rubric: IRubricDetail): { from: string; to: string } | null {
  const versions = rubric.versions ?? [];
  if (versions.length < 2) {
    return null;
  }
  const to = versions[0].semver;
  const active = rubric.latestPublishedSemver;
  return { from: active && active !== to ? active : versions[1].semver, to };
}

/**
 * What changed between two versions of a rubric: its settings, and the checks added, removed and changed.
 * For reviewing a draft against the version in use before publishing it.
 */
@Component({
  selector: 'app-rubric-compare',
  imports: [FormsModule, MatProgressSpinnerModule, RouterLink],
  templateUrl: './rubric-compare.component.html',
  styleUrls: ['./rubric-compare.component.scss']
})
export class RubricCompareComponent implements OnInit, OnDestroy {
  readonly statusLabel = statusLabel;
  readonly checkTypeLabel = checkTypeLabel;
  readonly checkSummary = checkSummary;

  rubricId = '';
  rubric: IRubricDetail | null = null;
  /** The version compared from (the older side) and to (the newer side). */
  from = '';
  to = '';
  before: IRubricVersionDetail | null = null;
  after: IRubricVersionDetail | null = null;
  comparison: RubricComparison | null = null;

  loading = false;
  notFound = false;
  error: string | null = null;

  private subscription?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private rubricService: RubricService,
    private changeDetector: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    this.subscription = combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(
      tap(([params, query]) => {
        const rubricId = params.get('rubricId') ?? '';
        if (rubricId !== this.rubricId) {
          this.rubric = null;
        }
        this.rubricId = rubricId;
        this.from = query.get('from') ?? '';
        this.to = query.get('to') ?? '';
        this.before = null;
        this.after = null;
        this.comparison = null;
        this.notFound = false;
        this.error = null;
        this.loading = true;
        this.changeDetector.markForCheck();
      }),
      switchMap(() => this.load().pipe(
        // Handled here, inside switchMap, so a failed load does not end the subscription to the route.
        catchError((error: RubricApiError) => {
          this.notFound = error.notFound;
          this.error = error.message;
          return of(null);
        })))
    ).subscribe(() => {
      this.loading = false;
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  get versions(): IRubricVersionSummary[] {
    return this.rubric?.versions ?? [];
  }

  get sameVersion(): boolean {
    return !!this.from && this.from === this.to;
  }

  /** "1.1.0 · Published, active", for the choice of a version. */
  describe(version: IRubricVersionSummary): string {
    const active = version.status === 'PUBLISHED' && version.semver === this.rubric?.latestPublishedSemver;
    return `${version.semver} · ${statusLabel(version.status)}${active ? ', active' : ''}`;
  }

  choose(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { from: this.from, to: this.to }
    });
  }

  swap(): void {
    [this.from, this.to] = [this.to, this.from];
    this.choose();
  }

  private load(): Observable<unknown> {
    const rubric$ = this.rubric ? of(this.rubric) : this.rubricService.getRubric(this.rubricId);
    return rubric$.pipe(
      switchMap(rubric => {
        this.rubric = rubric;
        if (!this.from || !this.to) {
          // The address does not say which two; choose, and put the choice in the address.
          const chosen = defaultComparison(rubric);
          if (chosen) {
            void this.router.navigate([], {
              relativeTo: this.route,
              queryParams: { from: this.from || chosen.from, to: this.to || chosen.to },
              replaceUrl: true
            });
          }
          return of(null);
        }
        if (this.from === this.to) {
          return of(null);
        }
        return forkJoin({
          before: this.rubricService.getVersion(this.rubricId, this.from),
          after: this.rubricService.getVersion(this.rubricId, this.to)
        }).pipe(map(({ before, after }) => {
          this.before = before;
          this.after = after;
          this.comparison = before.definition && after.definition
            ? compareDefinitions(before.definition, after.definition)
            : null;
          return null;
        }));
      }));
  }
}
