import { DatePipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, forkJoin, of, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricDetail,
  IRubricLifecycleEvent,
  IRubricSettings,
  IRubricVersionSummary
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import {
  actionLabel,
  dryRunLabel,
  pipelineUse,
  pipelineUseTitle,
  RubricPipelineUse,
  scoringLabel,
  statusLabel
} from '../rubric-display';

/** One rubric: its versions, newest first, and its history. */
@Component({
  selector: 'app-rubric-detail',
  imports: [
    DatePipe,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTabsModule,
    RouterLink
  ],
  templateUrl: './rubric-detail.component.html',
  styleUrls: ['./rubric-detail.component.scss']
})
export class RubricDetailComponent implements OnInit, OnDestroy {
  readonly versionColumns = ['semver', 'status', 'checks', 'scoring', 'dryRun', 'created', 'published', 'retired'];
  readonly eventColumns = ['occurredAt', 'action', 'semver', 'actor', 'checksum'];

  readonly statusLabel = statusLabel;
  readonly scoringLabel = scoringLabel;
  readonly dryRunLabel = dryRunLabel;
  readonly actionLabel = actionLabel;
  readonly pipelineUseTitle = pipelineUseTitle;

  rubricId = '';
  rubric: IRubricDetail | null = null;
  events: IRubricLifecycleEvent[] = [];
  /** Why the history could not be loaded. The versions are still shown when only this fails. */
  eventsError: string | null = null;
  /** How the reporting pipeline uses this rubric; null when it does not. */
  pipelineUse: RubricPipelineUse | null = null;

  loading = false;
  notFound = false;
  error: string | null = null;

  private settings: IRubricSettings | null = null;
  private subscription?: Subscription;
  private settingsSubscription?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private rubricService: RubricService,
    private changeDetector: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    // Not needed to show the rubric; without it the rubric is just not marked as in use.
    this.settingsSubscription = this.rubricService.getSettings().subscribe(settings => {
      this.settings = settings;
      this.pipelineUse = pipelineUse(this.settings, this.rubricId);
      this.changeDetector.markForCheck();
    });

    this.subscription = this.route.paramMap.pipe(
      tap(params => {
        this.rubricId = params.get('rubricId') ?? '';
        this.pipelineUse = pipelineUse(this.settings, this.rubricId);
        this.rubric = null;
        this.events = [];
        this.eventsError = null;
        this.notFound = false;
        this.error = null;
        this.loading = true;
        this.changeDetector.markForCheck();
      }),
      switchMap(() => forkJoin({
        rubric: this.rubricService.getRubric(this.rubricId),
        events: this.rubricService.getEvents(this.rubricId).pipe(
          catchError((error: RubricApiError) => {
            this.eventsError = error.message;
            return of([] as IRubricLifecycleEvent[]);
          }))
      }).pipe(
        // Handled here, inside switchMap, so a failed load does not end the subscription to the route.
        catchError((error: RubricApiError) => {
          this.notFound = error.notFound;
          this.error = error.message;
          return of(null);
        })))
    ).subscribe(result => {
      if (result) {
        this.rubric = result.rubric;
        this.events = result.events;
      }
      this.loading = false;
      // The answer arrives outside anything Angular watches for; say the page has changed.
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.settingsSubscription?.unsubscribe();
  }

  get versions(): IRubricVersionSummary[] {
    return this.rubric?.versions ?? [];
  }

  /** The version validation uses: the newest published one. */
  isActive(version: IRubricVersionSummary): boolean {
    return version.status === 'PUBLISHED' && version.semver === this.rubric?.latestPublishedSemver;
  }

  chipClass(version: IRubricVersionSummary): string {
    return `rubric-chip rubric-chip--${version.status.toLowerCase()}`;
  }
}
