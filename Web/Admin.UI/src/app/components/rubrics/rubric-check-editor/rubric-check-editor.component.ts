import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';

import {
  IRubricCheck,
  IRubricDefinition,
  IRubricSettings,
  RubricCheckType,
  RubricDimension,
  RubricSeverity
} from '../../../interfaces/validation/rubric.interface';
import { checkTypeLabel, dimensionLabel, severityLabel } from '../rubric-display';
import {
  checkKind,
  cloneDefinition,
  DEFAULT_SEVERITIES,
  findIssues,
  FORM_CHECK_TYPES,
  issuesFor,
  issuesForCheck,
  readOnlyReason,
  requiredDimension,
  RubricCheckKind,
  RubricIssue
} from '../rubric-draft';

/** The types a check inside a combined check can have. One that is itself combined is not edited here. */
const CHILD_TYPES: RubricCheckType[] = ['FHIRPATH', 'VALUESET', 'FHIR_CONFORMANCE', 'TERMINOLOGY'];

/** The fields of a check that have a place of their own on the form; a problem on any other is listed at the top. */
const PLACED_FIELDS = new Set([
  'id', 'dimension', 'severityOverride',
  'parameters.expression', 'parameters.code', 'parameters.path', 'parameters.valueSet', 'parameters.system',
  'parameters.profiles', 'parameters.valueSetWhitelistRegex', 'parameters.checks'
]);

/**
 * Edits one check of a draft. It works on a copy: nothing reaches the draft until Apply, and Cancel leaves
 * the draft as it was. A check the form cannot edit (a custom check, or one nested more than a level) is
 * shown as it is, read-only.
 */
@Component({
  selector: 'app-rubric-check-editor',
  imports: [FormsModule],
  templateUrl: './rubric-check-editor.component.html',
  styleUrls: ['./rubric-check-editor.component.scss']
})
export class RubricCheckEditorComponent implements OnChanges {
  /** The draft the check belongs to, for its dimensions and its other checks' ids. */
  @Input({ required: true }) definition!: IRubricDefinition;
  /** The check's place in the draft, or the number of checks for one being added. */
  @Input({ required: true }) index!: number;
  /** The check to edit. */
  @Input({ required: true }) check!: IRubricCheck;
  /** True when the check is being added, not changed. */
  @Input() adding = false;
  @Input() settings: IRubricSettings | null = null;
  /** Problems the service reported with this check, shown until the check is changed. */
  @Input() serverIssues: RubricIssue[] = [];

  @Output() applied = new EventEmitter<IRubricCheck>();
  @Output() cancelled = new EventEmitter<void>();

  readonly checkTypeLabel = checkTypeLabel;
  readonly dimensionLabel = dimensionLabel;
  readonly severityLabel = severityLabel;
  readonly childTypes = CHILD_TYPES;

  /** The copy being edited. */
  draft!: IRubricCheck;
  /** Profiles as the text box holds them: one to a line. */
  profilesText = '';
  /** Each child check's profiles, one to a line, by position. */
  childProfilesText: string[] = [];
  /** Problems are shown once Apply has been tried, so a new check is not red before anything is typed. */
  showIssues = false;
  issues: RubricIssue[] = [];
  /** The service's problems are about the check as it was saved; they are dropped at the first change. */
  private showServerIssues = true;

  constructor(private changeDetector: ChangeDetectorRef) {
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['check'] || changes['index']) {
      this.draft = JSON.parse(JSON.stringify(this.check));
      this.draft.parameters ??= {};
      // Every check being combined gets settings to bind to, even one that came without any.
      (this.draft.parameters.checks ?? []).forEach(child => child.parameters ??= {});
      this.profilesText = (this.draft.parameters.profiles ?? []).join('\n');
      this.childProfilesText = (this.draft.parameters.checks ?? []).map(child => (child.parameters?.profiles ?? []).join('\n'));
      // A check that already has problems is being opened to fix them, so they show straight away.
      this.showServerIssues = true;
      this.showIssues = !this.adding && (this.serverIssues.length > 0 || this.computeIssues().length > 0);
    }
    this.refreshIssues();
  }

  get readOnlyReason(): string | null {
    return readOnlyReason(this.check);
  }

  get checkJson(): string {
    return JSON.stringify(this.check, null, 2);
  }

  get kind(): RubricCheckKind {
    return checkKind(this.draft);
  }

  get types(): RubricCheckType[] {
    const supported = this.settings?.checkTypes?.filter(type => type.supported).map(type => type.type);
    const offered = FORM_CHECK_TYPES.filter(type => !supported || supported.includes(type));
    // A check keeps its type in the list even if the service no longer offers it for new checks.
    return offered.includes(this.draft.type) ? offered : [this.draft.type, ...offered];
  }

  get severities(): RubricSeverity[] {
    return this.settings?.severities?.length ? this.settings.severities : DEFAULT_SEVERITIES;
  }

  /** The dimension the check's type fixes, or null when it is a choice. */
  get fixedDimension(): RubricDimension | null {
    return requiredDimension(this.draft.type, this.settings);
  }

  /** The dimensions to choose from: the rubric's, and the check's own if the rubric has since dropped it. */
  get dimensions(): RubricDimension[] {
    const dimensions = [...this.definition.dimensions];
    if (this.draft.dimension && !dimensions.includes(this.draft.dimension)) {
      dimensions.unshift(this.draft.dimension);
    }
    return dimensions;
  }

  get combined(): boolean {
    return this.kind === 'combined';
  }

  errors(field: string): RubricIssue[] {
    return this.showIssues ? issuesFor(this.issues, field, this.index) : [];
  }

  /** Problems that have no field of their own on the form. */
  get otherErrors(): RubricIssue[] {
    if (!this.showIssues) {
      return [];
    }
    return issuesForCheck(this.issues, this.index)
      .filter(issue => !PLACED_FIELDS.has(issue.field) && !/^parameters\.checks\[\d+\]\./.test(issue.field));
  }

  childErrors(childIndex: number, field: string): RubricIssue[] {
    return this.errors(`parameters.checks[${childIndex}].${field}`);
  }

  childKind(child: IRubricCheck): RubricCheckKind {
    return checkKind(child);
  }

  onChanged(): void {
    this.showServerIssues = false;
    this.refreshIssues();
    this.changeDetector.markForCheck();
  }

  onTypeChanged(): void {
    // Each type has settings of its own, so the old type's are dropped, and the dimension follows the type.
    this.draft.parameters = {};
    this.profilesText = '';
    this.childProfilesText = [];
    this.draft.dimension = this.fixedDimension ?? (
      this.draft.dimension && this.definition.dimensions.includes(this.draft.dimension)
        ? this.draft.dimension
        : this.definition.dimensions[0]);
    this.onChanged();
  }

  setCombined(combined: boolean): void {
    if (combined === this.combined) {
      return;
    }
    this.draft.parameters = combined
      ? { matchMode: 'ANY', checks: [{ id: '', type: 'FHIRPATH', parameters: {} }] }
      : {};
    this.childProfilesText = combined ? [''] : [];
    this.onChanged();
  }

  onProfilesChanged(): void {
    this.draft.parameters!.profiles = this.lines(this.profilesText);
    this.onChanged();
  }

  onValidateCodingsChanged(value: string): void {
    if (value === '') {
      delete this.draft.parameters!.validateCodings;
    } else {
      this.draft.parameters!.validateCodings = value === 'true';
    }
    this.onChanged();
  }

  get validateCodings(): string {
    const value = this.draft.parameters?.validateCodings;
    return typeof value === 'boolean' ? String(value) : '';
  }

  addChild(): void {
    this.draft.parameters!.checks = [...(this.draft.parameters!.checks ?? []), { id: '', type: 'FHIRPATH', parameters: {} }];
    this.childProfilesText.push('');
    this.onChanged();
  }

  removeChild(childIndex: number): void {
    this.draft.parameters!.checks = (this.draft.parameters!.checks ?? []).filter((_, i) => i !== childIndex);
    this.childProfilesText.splice(childIndex, 1);
    this.onChanged();
  }

  onChildTypeChanged(childIndex: number): void {
    const child = this.draft.parameters!.checks![childIndex];
    child.parameters = {};
    this.childProfilesText[childIndex] = '';
    this.onChanged();
  }

  onChildProfilesChanged(childIndex: number): void {
    const child = this.draft.parameters!.checks![childIndex];
    child.parameters = { ...(child.parameters ?? {}), profiles: this.lines(this.childProfilesText[childIndex]) };
    this.onChanged();
  }

  /** Applies the change. With problems the first try only shows them; "Apply anyway" keeps the check as it is. */
  apply(evenWithIssues = false): void {
    this.refreshIssues();
    if (issuesForCheck(this.issues, this.index).length > 0 && !evenWithIssues) {
      this.showIssues = true;
      this.changeDetector.markForCheck();
      return;
    }
    this.applied.emit(this.draft);
  }

  cancel(): void {
    this.cancelled.emit();
  }

  private lines(text: string): string[] {
    return text.split(/\r?\n/).map(line => line.trim()).filter(line => line.length > 0);
  }

  private refreshIssues(): void {
    this.issues = this.showServerIssues && this.serverIssues.length > 0
      ? [...this.serverIssues, ...this.computeIssues().filter(local => !this.serverIssues.some(server => server.field === local.field))]
      : this.computeIssues();
  }

  /** The draft's problems with this check in its place, so "another check has this id" is caught too. */
  private computeIssues(): RubricIssue[] {
    if (!this.draft) {
      return [];
    }
    const withCheck = cloneDefinition(this.definition);
    withCheck.checks[this.index] = this.draft;
    return issuesForCheck(findIssues(withCheck, this.settings), this.index);
  }
}
