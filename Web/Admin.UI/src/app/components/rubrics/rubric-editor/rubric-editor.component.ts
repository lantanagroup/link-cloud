import { ChangeDetectorRef, Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, map, Observable, of, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricCheck,
  IRubricDefinition,
  IRubricSavePrecondition,
  IRubricSettings,
  RubricCheckType,
  RubricDimension
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { RubricCheckEditorComponent } from '../rubric-check-editor/rubric-check-editor.component';
import { EDITOR_NOTICE_STATE, takeEditorDefinition, takeEditorNotice } from './rubric-editor-state';
import { checkSummary, checkTypeLabel, dimensionLabel, severityLabel } from '../rubric-display';
import {
  bumpVersion,
  cleanDefinition,
  cloneDefinition,
  DEFAULT_DIMENSIONS,
  DEFAULT_ROLLUPS,
  DEFAULT_SCORING_POLICIES,
  findIssues,
  FORM_CHECK_TYPES,
  issuesFor,
  issuesForCheck,
  newCheck,
  newDefinition,
  nextVersion,
  placeServerError,
  readOnlyReason,
  rollupRequired,
  RubricIssue,
  sameDefinition,
  VersionPart
} from '../rubric-draft';

/** What the editor was opened to do. */
type EditorMode = 'new-rubric' | 'new-version' | 'edit';

/** What loading gives the editor to start from. */
interface EditorStart {
  definition: IRubricDefinition;
  /** The checksum of the draft being edited; absent when the version does not exist yet. */
  checksum?: string;
  /** The other versions the rubric has, so the next one can be numbered and a taken number caught. */
  existingVersions: string[];
  /** Why the version cannot be edited, when it cannot. */
  locked?: string;
}

/** A check being added or changed, until it is applied or cancelled. */
interface CheckEdit {
  index: number;
  check: IRubricCheck;
  adding: boolean;
}

const SCORING_LABELS: Record<string, string> = {
  'piqi-dimension-scorecard': 'Dimension scorecard',
  'piqi-check-scorecard': 'Check scorecard',
  'piqi-pass-fail': 'Pass/fail'
};

const ROLLUP_LABELS: Record<string, string> = {
  'worst-of': 'Worst of',
  'best-of': 'Best of',
  'pass-fail': 'Pass/fail',
  'majority': 'Majority',
  'all-must-pass': 'All must pass'
};

const TAB_DETAILS = 0;

const TAB_CHECKS = 1;

/**
 * Creates and changes a draft of a rubric, as a form. It opens for a new rubric, for a new version of a
 * rubric (a copy of an existing version to change), or for a draft that is already saved.
 *
 * Nothing reaches the service until Save draft. The form catches what it can as it is filled in; Validate
 * asks the service for the rest without saving. A save says what the editor believes the version's state
 * to be, so it is refused rather than replacing a draft someone else has changed in the meantime.
 */
@Component({
  selector: 'app-rubric-editor',
  imports: [
    FormsModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    RouterLink,
    RubricCheckEditorComponent
  ],
  templateUrl: './rubric-editor.component.html',
  styleUrls: ['./rubric-editor.component.scss']
})
export class RubricEditorComponent implements OnInit, OnDestroy {
  readonly checkTypeLabel = checkTypeLabel;
  readonly dimensionLabel = dimensionLabel;
  readonly severityLabel = severityLabel;
  readonly checkSummary = checkSummary;
  readonly readOnlyReason = readOnlyReason;

  mode: EditorMode = 'new-rubric';
  /** The rubric in the route; empty for a new rubric. */
  rubricId = '';
  definition: IRubricDefinition = newDefinition();
  settings: IRubricSettings | null = null;
  selectedTab = TAB_DETAILS;

  loading = false;
  loadError: string | null = null;
  /** Why this version cannot be edited: it is published or retired. */
  locked: string | null = null;

  /** The check being added or changed; the checks table gives way to its form while there is one. */
  editing: CheckEdit | null = null;
  newCheckType: RubricCheckType = 'FHIRPATH';
  newResourceType = '';
  newWorkflowTag = '';

  /** The form's own problems, kept up to date; shown once Validate or Save has been tried. */
  formIssues: RubricIssue[] = [];
  /** What the service reported the last time it was asked, until the draft is changed. */
  serverIssues: RubricIssue[] = [];
  showIssues = false;

  busy: 'validating' | 'saving' | null = null;
  /** What the last action achieved, to say so. */
  notice: string | null = null;
  /** Why the last action failed, when it is not a problem with a field. */
  failure: string | null = null;
  /** The draft was changed or deleted by someone else since it was opened; the save was refused. */
  staleDraft = false;

  /** The text pasted to replace the draft with, and why it could not be used. */
  pasteOpen = false;
  pasteText = '';
  pasteErrors: string[] = [];

  /** The draft as it was last loaded or saved, to tell whether there is anything unsaved. */
  private saved: IRubricDefinition = newDefinition();
  /** The checksum of the saved draft, sent back with the next save. */
  private savedChecksum: string | null = null;
  /** The version number the draft is saved under; changing it moves the draft. */
  private savedSemver: string | null = null;
  private existingVersions: string[] = [];
  private subscription?: Subscription;
  private action?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private rubricService: RubricService,
    private changeDetector: ChangeDetectorRef) {
  }

  // ------------------------------------------------------------------------------------------- loading

  ngOnInit(): void {
    this.subscription = this.route.paramMap.pipe(
      tap(() => {
        this.loading = true;
        this.loadError = null;
        this.locked = null;
        this.editing = null;
        this.clearOutcome();
        this.changeDetector.markForCheck();
      }),
      switchMap(params => {
        const rubricId = params.get('rubricId') ?? '';
        const semver = params.get('semver');
        this.rubricId = rubricId;
        this.mode = !rubricId ? 'new-rubric' : semver ? 'edit' : 'new-version';
        return forkJoin({
          start: this.loadStart(rubricId, semver, this.route.snapshot.queryParamMap.get('from')),
          settings: this.rubricService.getSettings()
        }).pipe(
          // Handled here, inside switchMap, so a failed load does not end the subscription to the route.
          catchError((error: RubricApiError) => {
            this.loadError = error.notFound
              ? (semver ? `The rubric "${rubricId}" has no version ${semver}.` : `There is no rubric with the id "${rubricId}".`)
              : error.message;
            return of(null);
          }));
      })
    ).subscribe(loaded => {
      if (loaded) {
        this.settings = loaded.settings;
        this.open(loaded.start);
        this.notice = takeEditorNotice();
      }
      this.loading = false;
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.action?.unsubscribe();
  }

  private loadStart(rubricId: string, semver: string | null, from: string | null): Observable<EditorStart> {
    // A definition handed over by the import, to be put right here. It opens unsaved, with its own version number.
    const handed = semver ? null : takeEditorDefinition();

    if (!rubricId) {
      return of({ definition: handed ?? newDefinition(), existingVersions: [] });
    }

    if (semver) {
      return forkJoin({
        version: this.rubricService.getVersion(rubricId, semver),
        // Only to number the next version and to catch a number that is taken; the draft opens without it.
        rubric: this.rubricService.getRubric(rubricId).pipe(catchError(() => of(null)))
      }).pipe(map(({ version, rubric }) => {
        if (!version.definition) {
          throw new RubricApiError('This version\'s definition could not be read.', 0);
        }
        return {
          definition: version.definition,
          checksum: version.checksum,
          existingVersions: (rubric?.versions ?? []).map(other => other.semver).filter(other => other !== version.semver),
          locked: version.status === 'DRAFT'
            ? undefined
            : `Version ${version.semver} is ${version.status.toLowerCase()} and cannot be changed. To change the rubric, create a new version from it.`
        };
      }));
    }

    // A new version starts as a copy: of the version asked for, else the active one, else the newest.
    return this.rubricService.getRubric(rubricId).pipe(switchMap(rubric => {
      const versions = (rubric.versions ?? []).map(version => version.semver);
      if (handed) {
        return of({ definition: { ...handed, id: rubricId }, existingVersions: versions });
      }
      const source = from ?? rubric.latestPublishedSemver ?? versions[0];
      if (!source) {
        throw new RubricApiError(`The rubric "${rubricId}" has no version to start from.`, 0);
      }
      return this.rubricService.getVersion(rubricId, source).pipe(map(version => {
        if (!version.definition) {
          throw new RubricApiError('The definition of the version to copy could not be read.', 0);
        }
        return { definition: { ...version.definition, semver: nextVersion(versions) }, existingVersions: versions };
      }));
    }));
  }

  private open(start: EditorStart): void {
    this.definition = cloneDefinition(start.definition);
    this.definition.title ??= '';
    this.definition.owner ??= '';
    this.locked = start.locked ?? null;
    this.existingVersions = start.existingVersions;
    this.savedChecksum = start.checksum ?? null;
    this.savedSemver = start.checksum ? start.definition.semver : null;
    // A copy for a new version, or a blank rubric, has nothing saved to compare with: it is unsaved as it stands.
    this.saved = start.checksum ? cloneDefinition(this.definition) : newDefinition();
    this.showIssues = false;
    this.serverIssues = [];
    this.selectedTab = TAB_DETAILS;
    this.refreshIssues();
  }

  // ------------------------------------------------------------------------------------------- state

  get title(): string {
    switch (this.mode) {
      case 'new-rubric':
        return 'New rubric';
      case 'new-version':
        return `New version of ${this.rubricId}`;
      default:
        return `${this.definition.title || this.rubricId} · ${this.savedSemver ?? this.definition.semver}`;
    }
  }

  /** True when the draft differs from what is saved, or has never been saved. */
  get dirty(): boolean {
    if (this.locked || this.loadError || this.loading) {
      return false;
    }
    if (!this.savedChecksum) {
      return this.mode !== 'new-rubric' || !sameDefinition(this.definition, newDefinition());
    }
    return !sameDefinition(this.definition, this.saved);
  }

  /** The id is the rubric's identity: it can only be chosen when the rubric is created. */
  get idEditable(): boolean {
    return this.mode === 'new-rubric' && !this.savedChecksum;
  }

  get dimensions(): RubricDimension[] {
    return this.settings?.dimensions?.length ? this.settings.dimensions : DEFAULT_DIMENSIONS;
  }

  get scoringPolicies(): { type: string; label: string }[] {
    const policies = this.settings?.scoringPolicies?.length ? this.settings.scoringPolicies : DEFAULT_SCORING_POLICIES;
    return policies.map(policy => ({ type: policy.type, label: SCORING_LABELS[policy.type] ?? policy.type }));
  }

  get rollups(): { value: string; label: string }[] {
    const rollups = this.settings?.rollups?.length ? this.settings.rollups : DEFAULT_ROLLUPS;
    return rollups.map(value => ({ value, label: ROLLUP_LABELS[value] ?? value }));
  }

  get needsRollup(): boolean {
    return rollupRequired(this.definition.scoringPolicy.type, this.settings);
  }

  get checkTypes(): RubricCheckType[] {
    const supported = this.settings?.checkTypes?.filter(type => type.supported).map(type => type.type);
    return FORM_CHECK_TYPES.filter(type => !supported || supported.includes(type));
  }

  get resourceTypes(): string[] {
    return this.settings?.fhirResourceTypes ?? [];
  }

  get fhirResources(): string[] {
    return this.definition.applicableContext?.fhirResources ?? [];
  }

  get workflowTags(): string[] {
    return this.definition.applicableContext?.workflowTags ?? [];
  }

  get definitionJson(): string {
    return JSON.stringify(cleanDefinition(this.definition), null, 2);
  }

  /** Every problem to show: the service's from the last time it was asked, and the form's own. */
  get issues(): RubricIssue[] {
    if (!this.showIssues) {
      return [];
    }
    return [...this.serverIssues, ...this.formIssues.filter(local => !this.serverIssues.some(
      server => server.field === local.field && server.checkIndex === local.checkIndex))];
  }

  errors(field: string): RubricIssue[] {
    return issuesFor(this.issues, field);
  }

  /** Problems with the list of checks as a whole, not with one check. */
  get checkListErrors(): RubricIssue[] {
    return this.errors('checks');
  }

  checkIssueCount(index: number): number {
    return issuesForCheck(this.issues, index).length;
  }

  get detailIssueCount(): number {
    return this.issues.filter(issue => issue.checkIndex === undefined && issue.field !== 'checks').length;
  }

  get checksIssueCount(): number {
    return this.issues.filter(issue => issue.checkIndex !== undefined || issue.field === 'checks').length;
  }

  serverIssuesForCheck(index: number): RubricIssue[] {
    return issuesForCheck(this.serverIssues, index);
  }

  /** Where a problem in the summary leads: the label of what it is about. */
  issueLabel(issue: RubricIssue): string {
    if (issue.checkIndex !== undefined) {
      return `Check ${this.definition.checks[issue.checkIndex]?.id || issue.checkIndex + 1}`;
    }
    return issue.path || 'Rubric';
  }

  /** Any change to the draft: what the service said about the old draft no longer applies. */
  onChanged(): void {
    this.serverIssues = [];
    this.clearOutcome();
    this.refreshIssues();
    this.changeDetector.markForCheck();
  }

  private refreshIssues(): void {
    this.formIssues = findIssues(this.definition, this.settings);
    const semver = this.definition.semver.trim();
    if (this.existingVersions.includes(semver) && !this.formIssues.some(issue => issue.field === 'semver')) {
      this.formIssues.push({
        path: 'semver', field: 'semver',
        message: `Version ${semver} of this rubric already exists. Choose another version number.`
      });
    }
  }

  private clearOutcome(): void {
    this.notice = null;
    this.failure = null;
    this.staleDraft = false;
  }

  // ------------------------------------------------------------------------------------------- details

  hasDimension(dimension: RubricDimension): boolean {
    return this.definition.dimensions.includes(dimension);
  }

  /** A dimension a check uses cannot be removed: the check would be left without one the rubric declares. */
  dimensionInUse(dimension: RubricDimension): boolean {
    return this.definition.checks.some(check => check.dimension === dimension);
  }

  toggleDimension(dimension: RubricDimension, on: boolean): void {
    const chosen = new Set(this.definition.dimensions);
    if (on) {
      chosen.add(dimension);
    } else {
      chosen.delete(dimension);
    }
    // Kept in the service's order, whatever order they were ticked in.
    this.definition.dimensions = this.dimensions.filter(candidate => chosen.has(candidate));
    this.onChanged();
  }

  onScoringPolicyChanged(): void {
    if (!this.needsRollup) {
      delete this.definition.scoringPolicy.rollup;
    } else if (!this.definition.scoringPolicy.rollup) {
      this.definition.scoringPolicy.rollup = this.rollups[0]?.value;
    }
    this.onChanged();
  }

  /** Sets the version a step above the newest other version of the rubric, so it never lands on one that exists. */
  setVersion(part: VersionPart): void {
    this.definition.semver = this.existingVersions.length
      ? nextVersion(this.existingVersions, part)
      : bumpVersion('0.0.0', part);
    this.onChanged();
  }

  addResourceType(): void {
    this.newResourceType = this.addToContext('fhirResources', this.newResourceType);
  }

  addWorkflowTag(): void {
    this.newWorkflowTag = this.addToContext('workflowTags', this.newWorkflowTag);
  }

  removeFromContext(list: 'fhirResources' | 'workflowTags', value: string): void {
    const context = this.definition.applicableContext ?? {};
    context[list] = (context[list] ?? []).filter(candidate => candidate !== value);
    this.definition.applicableContext = context;
    this.onChanged();
  }

  /** Adds what was typed to a list, once, and gives back what the box should hold afterwards. */
  private addToContext(list: 'fhirResources' | 'workflowTags', typed: string): string {
    const value = typed.trim();
    if (!value) {
      return '';
    }
    const context = this.definition.applicableContext ?? {};
    if (!(context[list] ?? []).includes(value)) {
      context[list] = [...(context[list] ?? []), value];
      this.definition.applicableContext = context;
      this.onChanged();
    }
    return '';
  }

  // ------------------------------------------------------------------------------------------- checks

  addCheck(): void {
    this.editing = {
      index: this.definition.checks.length,
      check: newCheck(this.newCheckType, this.definition, this.settings),
      adding: true
    };
  }

  editCheck(index: number): void {
    this.editing = { index, check: this.definition.checks[index], adding: false };
  }

  applyCheck(check: IRubricCheck): void {
    if (!this.editing) {
      return;
    }
    const checks = [...this.definition.checks];
    checks[this.editing.index] = check;
    this.definition.checks = checks;
    this.editing = null;
    this.onChanged();
  }

  cancelCheck(): void {
    this.editing = null;
  }

  duplicateCheck(index: number): void {
    const copy: IRubricCheck = JSON.parse(JSON.stringify(this.definition.checks[index]));
    const taken = new Set(this.definition.checks.map(check => check.id));
    let id = `${copy.id}-copy`;
    for (let n = 2; taken.has(id); n++) {
      id = `${copy.id}-copy-${n}`;
    }
    copy.id = id;
    const checks = [...this.definition.checks];
    checks.splice(index + 1, 0, copy);
    this.definition.checks = checks;
    this.onChanged();
  }

  deleteCheck(index: number): void {
    this.definition.checks = this.definition.checks.filter((_, i) => i !== index);
    this.onChanged();
  }

  /** Moves a check one place earlier or later. The checks run in the order they are listed. */
  moveCheck(index: number, by: -1 | 1): void {
    const target = index + by;
    if (target < 0 || target >= this.definition.checks.length) {
      return;
    }
    const checks = [...this.definition.checks];
    [checks[index], checks[target]] = [checks[target], checks[index]];
    this.definition.checks = checks;
    this.onChanged();
  }

  setCheckEnabled(index: number, enabled: boolean): void {
    const checks = [...this.definition.checks];
    checks[index] = { ...checks[index], enabled };
    this.definition.checks = checks;
    this.onChanged();
  }

  // ------------------------------------------------------------------------------------------- pasting

  /** Replaces the draft with a definition pasted as YAML or JSON. The service reads it; the form never parses YAML. */
  loadPasted(): void {
    const text = this.pasteText.trim();
    if (!text || this.busy) {
      return;
    }
    this.busy = 'validating';
    this.pasteErrors = [];
    this.action = this.rubricService.validateText(text, text.startsWith('{') ? 'json' : 'yaml').subscribe({
      next: result => {
        this.busy = null;
        if (!result.definition) {
          this.pasteErrors = result.errors.length ? result.errors : ['This could not be read as a rubric definition.'];
        } else if (!this.idEditable && result.definition.id !== this.rubricId) {
          this.pasteErrors = [
            `This definition is for the rubric "${result.definition.id}", not "${this.rubricId}". To bring in another rubric, use Import.`];
        } else {
          this.definition = cloneDefinition(result.definition);
          this.definition.title ??= '';
          this.definition.owner ??= '';
          this.pasteOpen = false;
          this.pasteText = '';
          this.onChanged();
          // What the service found wrong with it is shown where it belongs, as after Validate.
          this.serverIssues = result.errors.map(error => placeServerError(error, this.definition));
          this.showIssues = this.serverIssues.length > 0 || this.formIssues.length > 0;
          this.notice = 'The draft was replaced with the pasted definition. It is not saved yet.';
        }
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.busy = null;
        this.pasteErrors = [error.message];
        this.changeDetector.markForCheck();
      }
    });
  }

  // ------------------------------------------------------------------------------------------- actions

  /** Checks the draft against every rule without saving it: the form's own first, then the service's. */
  validate(): void {
    if (this.busy) {
      return;
    }
    this.clearOutcome();
    this.showIssues = true;
    this.refreshIssues();
    this.changeDetector.markForCheck();
    if (this.formIssues.length > 0) {
      this.goToFirstIssue();
      return;
    }

    this.busy = 'validating';
    this.action = this.rubricService.validate(cleanDefinition(this.definition)).subscribe({
      next: result => {
        this.busy = null;
        this.serverIssues = result.errors.map(error => placeServerError(error, this.definition));
        if (result.valid) {
          this.notice = 'No problems found. The draft is ready to save.';
        } else {
          this.goToFirstIssue();
        }
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.busy = null;
        this.failure = error.message;
        this.changeDetector.markForCheck();
      }
    });
  }

  /** Saves the draft. `overwrite` replaces the saved draft whatever it holds, after a refused save. */
  save(overwrite = false): void {
    if (this.busy) {
      return;
    }
    this.clearOutcome();
    this.showIssues = true;
    this.refreshIssues();
    this.changeDetector.markForCheck();
    if (this.formIssues.length > 0) {
      this.goToFirstIssue();
      return;
    }

    const definition = cleanDefinition(this.definition);
    // A draft saved under another version number moves: it is saved as a new version, then the old one is deleted.
    const moving = this.savedSemver !== null && this.savedSemver !== definition.semver;
    const precondition: IRubricSavePrecondition = overwrite
      ? {}
      : this.savedChecksum && !moving
        ? { expectedChecksum: this.savedChecksum }
        : { mustNotExist: true };
    const previousSemver = this.savedSemver;

    this.busy = 'saving';
    this.action = this.rubricService.saveDraft(definition, precondition).pipe(
      switchMap(version => {
        if (!moving || !previousSemver) {
          return of({ version, leftBehind: null as string | null });
        }
        return this.rubricService.deleteDraft(definition.id, previousSemver).pipe(
          map(() => ({ version, leftBehind: null as string | null })),
          // The draft is saved under its new number; only tidying up the old one failed.
          catchError(() => of({ version, leftBehind: previousSemver })));
      })
    ).subscribe({
      next: ({ version, leftBehind }) => {
        this.busy = null;
        this.definition = cloneDefinition({ ...definition, semver: version.semver });
        this.definition.title ??= '';
        this.definition.owner ??= '';
        this.saved = cloneDefinition(this.definition);
        this.savedChecksum = version.checksum;
        // The number the draft moved away from is free again unless its draft could not be deleted.
        this.existingVersions = this.existingVersions.filter(other => other !== version.semver);
        if (leftBehind) {
          this.existingVersions = [...this.existingVersions, leftBehind];
        }
        this.savedSemver = version.semver;
        this.rubricId = definition.id;
        this.mode = 'edit';
        this.serverIssues = [];
        this.showIssues = false;
        this.refreshIssues();
        this.notice = leftBehind
          ? `Draft saved as version ${version.semver}. The draft under ${leftBehind} could not be deleted; delete it from the rubric's page.`
          : moving
            ? `Draft saved as version ${version.semver}; the draft under ${previousSemver} was deleted.`
            : 'Draft saved.';
        // The address follows the draft, so a refresh reopens it and a second save updates it. Moving the
        // address reopens the editor on the saved draft, so what was achieved is handed across to be shown there.
        const address = ['/rubrics', definition.id, 'versions', version.semver, 'edit'];
        if (!this.router.isActive(this.router.createUrlTree(address), { paths: 'exact', queryParams: 'ignored', fragment: 'ignored', matrixParams: 'ignored' })) {
          void this.router.navigate(address, { replaceUrl: true, state: { [EDITOR_NOTICE_STATE]: this.notice } });
        }
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.busy = null;
        if (error.errors.length > 0) {
          this.serverIssues = error.errors.map(text => placeServerError(text, this.definition));
          this.goToFirstIssue();
        } else if (error.preconditionFailed && precondition.expectedChecksum) {
          this.staleDraft = true;
        } else if (error.preconditionFailed) {
          this.serverIssues = [{
            path: 'semver', field: 'semver',
            message: `Version ${definition.semver} of this rubric already exists. Choose another version number.`
          }];
          this.goToFirstIssue();
        } else if (error.conflict) {
          this.serverIssues = [{
            path: 'semver', field: 'semver',
            message: `Version ${definition.semver} is published or retired and cannot be replaced. Choose another version number.`
          }];
          this.goToFirstIssue();
        } else {
          this.failure = error.message;
        }
        this.changeDetector.markForCheck();
      }
    });
  }

  /** Drops what was changed here and opens the draft as it is now saved. */
  reload(): void {
    if (!this.savedSemver) {
      return;
    }
    this.loading = true;
    this.clearOutcome();
    this.action = this.rubricService.getVersion(this.rubricId, this.savedSemver).subscribe({
      next: version => {
        this.loading = false;
        if (version.definition) {
          this.open({
            definition: version.definition,
            checksum: version.checksum,
            existingVersions: this.existingVersions,
            locked: version.status === 'DRAFT' ? undefined
              : `Version ${version.semver} is now ${version.status.toLowerCase()} and cannot be changed.`
          });
        }
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.loading = false;
        this.loadError = error.notFound ? `Version ${this.savedSemver} no longer exists; it was deleted.` : error.message;
        this.changeDetector.markForCheck();
      }
    });
  }

  /** Opens the tab that holds the first problem, and the check when it is about one. */
  goToIssue(issue: RubricIssue): void {
    if (issue.checkIndex !== undefined) {
      this.selectedTab = TAB_CHECKS;
      this.editCheck(issue.checkIndex);
    } else if (issue.field === 'checks') {
      this.selectedTab = TAB_CHECKS;
      this.editing = null;
    } else {
      this.selectedTab = TAB_DETAILS;
      this.editing = null;
    }
  }

  private goToFirstIssue(): void {
    const first = this.issues[0];
    if (!first) {
      return;
    }
    // To the tab only: opening a check's form unasked would hide the list of what else is wrong.
    this.selectedTab = first.checkIndex !== undefined || first.field === 'checks' ? TAB_CHECKS : TAB_DETAILS;
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty) {
      event.preventDefault();
    }
  }
}
