import { DatePipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, merge, Observable, of, Subject, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricCheck,
  IRubricDefinition,
  IRubricDetail,
  IRubricSettings,
  IRubricVersionDetail,
  RubricDefinitionFormat
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import {
  checkSettings,
  CheckSetting,
  checkSummary,
  checkTypeLabel,
  dimensionLabel,
  dryRunLabel,
  isCheckEnabled,
  isCombinedCheck,
  orderedChecks,
  scoringLabel,
  severityLabel,
  statusLabel
} from '../rubric-display';
import {
  ACTION_NAMES,
  actionsFor,
  deletingRemovesRubric,
  planAction,
  RubricAction,
  RubricActionPlan
} from '../rubric-lifecycle';

/** The query parameter another page uses to open a confirmation here: `?do=publish`. */
export const VERSION_ACTION_PARAM = 'do';

/**
 * One version of a rubric, read-only: what it is set to, the checks it runs, and its definition. It is also
 * where a version is published, retired or deleted, each behind a confirmation that says what will happen.
 */
@Component({
  selector: 'app-rubric-version-view',
  imports: [
    DatePipe,
    FormsModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTabsModule,
    RouterLink
  ],
  templateUrl: './rubric-version-view.component.html',
  styleUrls: ['./rubric-version-view.component.scss']
})
export class RubricVersionViewComponent implements OnInit, OnDestroy {
  readonly checkColumns = ['order', 'id', 'type', 'dimension', 'severity', 'enabled', 'summary'];

  readonly statusLabel = statusLabel;
  readonly scoringLabel = scoringLabel;
  readonly dryRunLabel = dryRunLabel;
  readonly checkTypeLabel = checkTypeLabel;
  readonly dimensionLabel = dimensionLabel;
  readonly severityLabel = severityLabel;
  readonly checkSummary = checkSummary;
  readonly isCheckEnabled = isCheckEnabled;
  readonly actionNames = ACTION_NAMES;

  rubricId = '';
  semver = '';
  version: IRubricVersionDetail | null = null;
  checks: IRubricCheck[] = [];
  definitionJson = '';
  expandedCheckId: string | null = null;
  copied = false;
  /** The format being exported, while the file is being fetched. */
  exporting: RubricDefinitionFormat | null = null;
  /** Why the last export failed, when it did. */
  exportError: string | null = null;

  /** The rubric this is a version of; null when it could not be read, which only makes a confirmation say less. */
  rubric: IRubricDetail | null = null;
  /** The confirmation that is open; null when none is. */
  plan: RubricActionPlan | null = null;
  /** What has been typed where a confirmation asks for the rubric's id. */
  confirmText = '';
  /** True while the action is being carried out. */
  acting = false;
  /** Why the action was refused, when it was. */
  actionError: string | null = null;
  /** What was just done, said once it is. */
  notice: string | null = null;

  loading = false;
  notFound = false;
  error: string | null = null;

  private settings: IRubricSettings | null = null;
  private requestedAction: string | null = null;
  private readonly reload = new Subject<void>();
  private subscription?: Subscription;
  private querySubscription?: Subscription;
  private settingsSubscription?: Subscription;
  private actionRequest?: Subscription;
  private exportRequest?: Subscription;
  private copiedTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private rubricService: RubricService,
    private changeDetector: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    // Not needed to show the version; without it a confirmation cannot say what this environment requires.
    this.settingsSubscription = this.rubricService.getSettings().subscribe(settings => {
      this.settings = settings;
      this.changeDetector.markForCheck();
    });

    const opened = this.route.paramMap.pipe(
      tap(params => {
        this.rubricId = params.get('rubricId') ?? '';
        this.semver = params.get('semver') ?? '';
        this.version = null;
        this.rubric = null;
        this.checks = [];
        this.definitionJson = '';
        this.expandedCheckId = null;
        this.exportRequest?.unsubscribe();
        this.exporting = null;
        this.exportError = null;
        this.closePlan();
        this.notice = null;
        this.notFound = false;
        this.error = null;
        this.loading = true;
        this.changeDetector.markForCheck();
      }));

    this.subscription = merge(opened, this.reload).pipe(
      switchMap(() => forkJoin({
        version: this.rubricService.getVersion(this.rubricId, this.semver),
        rubric: this.rubricService.getRubric(this.rubricId).pipe(catchError(() => of(null)))
      }).pipe(
        // Handled here, inside switchMap, so a failed load does not end the subscription to the route.
        catchError((error: RubricApiError) => {
          this.notFound = error.notFound;
          this.error = error.message;
          return of(null);
        })))
    ).subscribe(result => {
      if (result) {
        this.version = result.version;
        this.rubric = result.rubric;
        this.checks = orderedChecks(result.version.definition);
        this.definitionJson = result.version.definition ? JSON.stringify(result.version.definition, null, 2) : '';
        this.openRequestedAction();
      }
      this.loading = false;
      // The answer arrives outside anything Angular watches for; say the page has changed.
      this.changeDetector.markForCheck();
    });

    // Another page asks for a confirmation with ?do=publish; it opens once the version is here.
    this.querySubscription = this.route.queryParamMap.subscribe(params => {
      this.requestedAction = params.get(VERSION_ACTION_PARAM);
      this.openRequestedAction();
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.querySubscription?.unsubscribe();
    this.settingsSubscription?.unsubscribe();
    this.exportRequest?.unsubscribe();
    this.actionRequest?.unsubscribe();
    clearTimeout(this.copiedTimer);
  }

  /** What this version can have done to it. */
  get actions(): RubricAction[] {
    return actionsFor(this.version?.status);
  }

  /** True when this is the version validation uses. */
  get isActive(): boolean {
    return this.version?.status === 'PUBLISHED' && this.version.semver === this.rubric?.latestPublishedSemver;
  }

  /**
   * The version this one is most usefully compared with: the active one, or failing that the one before
   * it. Null when the rubric has no other version.
   */
  get compareWith(): string | null {
    const versions = this.rubric?.versions ?? [];
    const active = this.rubric?.latestPublishedSemver;
    if (active && active !== this.version?.semver) {
      return active;
    }
    const index = versions.findIndex(version => version.semver === this.version?.semver);
    return versions[index + 1]?.semver ?? versions.find(version => version.semver !== this.version?.semver)?.semver ?? null;
  }

  /** True when the open confirmation can be confirmed as things stand. */
  get canConfirm(): boolean {
    return !!this.plan && !this.plan.blocked && !this.acting
      && (this.plan.typeToConfirm === null || this.confirmText.trim() === this.plan.typeToConfirm);
  }

  /** Opens the confirmation for an action. Nothing is done until it is confirmed. */
  open(action: RubricAction): void {
    if (!this.version || !this.actions.includes(action)) {
      return;
    }
    this.plan = planAction(action, this.rubricId, this.version, this.rubric, this.settings, this.enabledCheckCount);
    this.confirmText = '';
    this.actionError = null;
    this.notice = null;
    this.changeDetector.markForCheck();
  }

  cancelAction(): void {
    this.actionRequest?.unsubscribe();
    this.closePlan();
    this.forgetRequestedAction();
    this.changeDetector.markForCheck();
  }

  confirm(): void {
    if (!this.plan || !this.version || !this.canConfirm) {
      return;
    }
    const action = this.plan.action;
    const semver = this.version.semver;
    this.acting = true;
    this.actionError = null;

    const request: Observable<unknown> = action === 'publish'
      ? this.rubricService.publish(this.rubricId, semver)
      : action === 'retire'
        ? this.rubricService.retire(this.rubricId, semver)
        : this.rubricService.deleteDraft(this.rubricId, semver);

    this.actionRequest = request.subscribe({
      next: () => {
        const removesRubric = deletingRemovesRubric(this.rubric);
        this.closePlan();
        if (action === 'delete') {
          // The version is gone, so there is nothing here to show.
          void this.router.navigate(removesRubric ? ['/rubrics'] : ['/rubrics', this.rubricId]);
          return;
        }
        this.notice = action === 'publish'
          ? `Version ${semver} is published and is now the active version.`
          : `Version ${semver} is retired.`;
        this.forgetRequestedAction();
        this.reload.next();
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.acting = false;
        this.actionError = error.message;
        this.changeDetector.markForCheck();
      }
    });
  }

  private closePlan(): void {
    this.plan = null;
    this.confirmText = '';
    this.acting = false;
    this.actionError = null;
  }

  /** Opens the confirmation the address asks for, when the version allows it. */
  private openRequestedAction(): void {
    const action = this.requestedAction as RubricAction | null;
    if (!action || !this.version || this.plan) {
      return;
    }
    if (this.actions.includes(action)) {
      this.open(action);
    } else {
      this.forgetRequestedAction();
    }
  }

  /** Takes the request out of the address, so a refresh or the Back button does not ask again. */
  private forgetRequestedAction(): void {
    if (this.requestedAction === null) {
      return;
    }
    this.requestedAction = null;
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [VERSION_ACTION_PARAM]: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  get definition(): IRubricDefinition | undefined {
    return this.version?.definition;
  }

  get title(): string {
    return this.definition?.title || this.rubricId;
  }

  get enabledCheckCount(): number {
    return this.checks.filter(isCheckEnabled).length;
  }

  get statusChipClass(): string {
    return `rubric-chip rubric-chip--${(this.version?.status ?? '').toLowerCase()}`;
  }

  severityChipClass(check: IRubricCheck): string {
    return `rubric-chip rubric-chip--${(check.severityOverride ?? '').toLowerCase()}`;
  }

  toggleCheck(check: IRubricCheck): void {
    this.expandedCheckId = this.expandedCheckId === check.id ? null : check.id;
  }

  /** A check's settings as label and value, in the order they were written. Child checks are listed apart. */
  settingsOf(check: IRubricCheck): CheckSetting[] {
    return checkSettings(check);
  }

  /** The child checks of a combined value-set check; none for any other check. */
  childrenOf(check: IRubricCheck): IRubricCheck[] {
    return isCombinedCheck(check) ? check.parameters?.checks ?? [] : [];
  }

  /**
   * Downloads the definition as a file that registering a version accepts as it is. The service writes
   * the file, so the YAML is the service's own and not this screen's idea of it.
   */
  export(format: RubricDefinitionFormat): void {
    if (!this.version || this.exporting) {
      return;
    }
    const semver = this.version.semver;
    this.exporting = format;
    this.exportError = null;

    this.exportRequest = this.rubricService.exportDefinition(this.rubricId, semver, format).subscribe({
      next: text => {
        this.download(text, `${this.rubricId}-${semver}.${format}`,
          format === 'yaml' ? 'application/yaml' : 'application/json');
        this.exporting = null;
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.exportError = error.message;
        this.exporting = null;
        this.changeDetector.markForCheck();
      }
    });
  }

  private download(text: string, fileName: string, type: string): void {
    const url = URL.createObjectURL(new Blob([text], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  async copyDefinition(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.definitionJson);
      this.copied = true;
      clearTimeout(this.copiedTimer);
      this.copiedTimer = setTimeout(() => {
        this.copied = false;
        this.changeDetector.markForCheck();
      }, 2000);
    } catch {
      // The clipboard is unavailable (an insecure origin, or permission refused). The text is on the
      // page and can be selected, so there is nothing more to do.
      this.copied = false;
    }
    this.changeDetector.markForCheck();
  }
}
