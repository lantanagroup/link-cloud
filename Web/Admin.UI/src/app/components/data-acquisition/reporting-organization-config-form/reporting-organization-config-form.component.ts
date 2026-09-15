import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { Subject, Subscription, forkJoin, merge, of, catchError, debounceTime, distinctUntilChanged, map, switchMap } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { FormMode } from 'src/app/models/FormMode.enum';
import { IVendor } from 'src/app/interfaces/tenant/vendor-interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {
  ICreateOrganizationLocationConditionModel,
  ICreateOrganizationLocationConfigurationModel,
  IOrganizationLocationConfigurationModel,
  IUpdateOrganizationLocationConfigurationModel
} from '../../../interfaces/data-acquisition/organization-location-config-model.interface';
import { IQueryPlanModel } from '../../../interfaces/data-acquisition/query-plan-model.interface';

@Component({
  selector: 'app-reporting-organization-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatRadioModule,
    ReactiveFormsModule,
    MatTooltipModule
  ],
  templateUrl: './reporting-organization-config-form.component.html',
  styleUrls: ['./reporting-organization-config-form.component.scss']
})
export class ReportingOrganizationConfigFormComponent implements OnInit, OnChanges, OnDestroy {
  @Input() item!: IOrganizationLocationConfigurationModel;

  @Input() vendor?: IVendor;

  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) {
    if (v !== null) this._viewOnly = v;
  }

  get viewOnly() {
    return this._viewOnly;
  }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  configForm!: FormGroup;

  // Save error shown inline at the bottom of the dialog (no snackbar / global toast).
  saveError: string | null = null;

  // Frequency plans for this facility whose initial queries are missing an Encounter and/or
  // Location query. While IsActive is on, activation is blocked until these are resolved.
  // Populated client-side so the form can pre-validate before the user submits.
  nonCompliantPlans: { type: string; missingEncounter: boolean; missingLocation: boolean }[] = [];
  private fetchedPlansForFacility: string | null = null;

  // Every frequency plan type a facility can have (mirrors the backend Frequency enum).
  private static readonly FrequencyTypes = ['Discharge', 'Daily', 'Weekly', 'Monthly', 'Adhoc'];

  // FHIRPath validation findings shown inline under the expression.
  // Errors block save; warnings are advisory. (Validation is currently stubbed — see the service.)
  fhirPathErrors: string[] = [];
  fhirPathWarnings: string[] = [];
  validatingFhirPath = false;

  // True when the saved rules were built for the other EHR vendor (e.g. an Epic rule on a
  // Cerner facility). Advisory only — nothing is deleted.
  vendorMismatch = false;

  // Every generated/typed expression for this form is applying at the Location resource.
  private static readonly FhirPathResourceType = 'Location';

  // All builder fields a match group can hold (across every method).
  private static readonly BuilderFields = [
    'identifierSystem',
    'identifierCode',
    'organizationId',
    'locationTypeCode',
    'locationAlias'
  ];

  // Which builder fields are mandatory per method. Only the fields actually rendered for
  // the active method are required; everything else is cleared so hidden fields never
  // block Save. Location Alias is intentionally absent — it stays optional by design.
  private static readonly RequiredFieldsByMethod: Record<string, string[]> = {
    identifier: ['identifierSystem', 'identifierCode'],
    managingOrg: ['organizationId'],
    locationType: ['locationTypeCode']
  };

  /** Required validator that also rejects whitespace-only values (which produce empty FHIRPath operands). */
  private static requiredNonBlank(control: AbstractControl): ValidationErrors | null {
    return (control.value ?? '').toString().trim().length > 0 ? null : { required: true };
  }

  // Debounced channel that runs the current expression through FHIRPath validation.
  private fhirPathValidation$ = new Subject<string>();

  // Lifetime subscriptions (validation pipeline, manual-edit watcher, form-status watcher).
  private subscriptions = new Subscription();

  // Track per-row valueChanges subscriptions for the builder fields.
  private builderSubscriptions: Subscription[] = [];

  // Remember each method's entries (and the Custom FHIRPath text) so switching the
  // radio away and back restores what you had instead of wiping it. In-memory only.
  private methodEntriesCache: Record<string, any[]> = {};
  private manualCache = '';
  private previousMethod: string | null = null;

  constructor(
    private dataAcquisitionService: DataAcquisitionService,
    private fb: FormBuilder
  ) {
    this.configForm = this.fb.group({
      facilityId: this.fb.control('', Validators.required),
      description: this.fb.control(''),
      isActive: this.fb.control(true),
      // Method per config (radio): a builder method (managingOrg / locationType /
      // identifier) whose matches are OR'd into the FHIRPath, or 'manual' to type a
      // custom FHIRPath directly.
      setupMethod: this.fb.control('identifier', Validators.required),
      // The single combined FHIRPath that is saved. A read-only preview generated from
      // the match entries in builder mode; directly editable in manual.
      // `required` only prevents an empty save.
      fhirPath: this.fb.control('', Validators.required),
      conditions: this.fb.array([])
    });
  }

  ngOnInit(): void {
    // Wire validation before the first buildForm() so the initial expression gets validated.
    this.setupFhirPathValidation();

    this.buildForm();

    // Emit on value AND status changes so the Save button reliably reflects validity —
    // e.g. it stays disabled when the required Custom FHIRPath hasn't been entered.
    this.subscriptions.add(
      merge(this.configForm.valueChanges, this.configForm.statusChanges).subscribe(() => {
        this.emitValidity();
        // Clear a stale save error once the user starts changing the form.
        this.saveError = null;
      })
    );
  }

  /**
   * Whether Save should be blocked: when the form is invalid, or — in Custom FHIRPath mode —
   * while validation is running or has returned errors. Builder methods depend only on form validity.
   */
  private get isSaveDisabled(): boolean {
    if (this.configForm.invalid) return true;
    // Activation requires every frequency plan's initial queries to include Encounter + Location.
    if (this.isActiveControl.value && this.nonCompliantPlans.length > 0) return true;
    if (this.setupMethodControl.value === 'manual') {
      return this.validatingFhirPath || this.fhirPathErrors.length > 0;
    }
    return false;
  }

  /**
   * Message shown when the user has Active on but one or more frequency plans are missing the
   * required Encounter/Location queries. Mirrors the backend activation validation wording.
   * Returns null when activation is not blocked.
   */
  get activationPrerequisiteError(): string | null {
    if (!this.isActiveControl.value || this.nonCompliantPlans.length === 0) return null;

    if (this.nonCompliantPlans.length === 1) {
      const plan = this.nonCompliantPlans[0];
      const requirement = (plan.missingEncounter && plan.missingLocation)
        ? 'must include both an Encounter and a Location query'
        : plan.missingEncounter
          ? 'must include an Encounter query'
          : 'must include a Location query';
      return `Cannot enable location resolution: the ${plan.type} query plan's initial queries ${requirement}.`;
    }

    const list = ReportingOrganizationConfigFormComponent.formatFrequencyList(
      this.nonCompliantPlans.map(p => p.type));
    return `Cannot enable location resolution: the ${list} query plans are missing required ` +
      `Encounter/Location queries in their initial queries.`;
  }

  /**
   * Loads the facility's frequency plans and records which are missing Encounter/Location so the
   * form can block activation client-side. The backend remains the authority; failures here are
   * non-blocking (the rule is still enforced on save).
   */
  private loadFacilityQueryPlans(): void {
    const facilityId = this.item?.facilityId ?? this.facilityIdControl.value;
    if (!facilityId || facilityId === this.fetchedPlansForFacility) return;
    this.fetchedPlansForFacility = facilityId;

    this.subscriptions.add(
      forkJoin(
        ReportingOrganizationConfigFormComponent.FrequencyTypes.map(type =>
          this.dataAcquisitionService.getQueryPlanConfiguration(facilityId, type)
            .pipe(catchError(() => of(null)))
        )
      ).subscribe(plans => {
        this.nonCompliantPlans = plans
          .filter((p): p is IQueryPlanModel => !!p)
          .map(p => this.evaluatePlanCompliance(p))
          .filter(r => r.missingEncounter || r.missingLocation);
        this.emitValidity();
      })
    );
  }

  private evaluatePlanCompliance(plan: IQueryPlanModel): { type: string; missingEncounter: boolean; missingLocation: boolean } {
    const queries = plan.initialQueries ? Object.values(plan.initialQueries) : [];
    const hasEncounter = queries.some(q => (q?.resourceType || '').toLowerCase() === 'encounter');
    const hasLocation = queries.some(q => (q?.resourceType || '').toLowerCase() === 'location');
    return { type: plan.type, missingEncounter: !hasEncounter, missingLocation: !hasLocation };
  }

  private static formatFrequencyList(frequencies: string[]): string {
    if (frequencies.length <= 1) return frequencies[0] ?? '';
    if (frequencies.length === 2) return `${frequencies[0]} and ${frequencies[1]}`;
    return `${frequencies.slice(0, -1).join(', ')}, and ${frequencies[frequencies.length - 1]}`;
  }

  /** Pushes the current save-disabled state out so the dialog's Save button stays in sync. */
  private emitValidity(): void {
    this.formValueChanged.emit(this.isSaveDisabled);
  }

  ngOnDestroy(): void {
    this.clearBuilderSubscriptions();
    this.subscriptions.unsubscribe();
    this.fhirPathValidation$.complete();
  }

  /**
   * Sets up the debounced FHIRPath validation pipeline and the manual-edit watcher.
   * Typing in Custom mode pushes through the control's valueChanges here.
   */
  private setupFhirPathValidation(): void {
    this.subscriptions.add(
      this.fhirPathValidation$
        .pipe(
          map(expr => (expr ?? '').trim()),
          debounceTime(400),
          distinctUntilChanged(),
          switchMap(expr => {
            this.fhirPathErrors = [];
            this.fhirPathWarnings = [];
            if (!expr) {
              this.validatingFhirPath = false;
              this.emitValidity();
              return of(null);
            }
            this.validatingFhirPath = true;
            // Disable Save while the check is in flight so an unvalidated expression can't be saved.
            this.emitValidity();
            return this.dataAcquisitionService
              .validateFhirPath(ReportingOrganizationConfigFormComponent.FhirPathResourceType, expr)
              .pipe(catchError(() => of(null)));
          })
        )
        .subscribe(result => {
          this.validatingFhirPath = false;
          // A result that lands after the user switched away from Custom mode describes an
          // expression that is no longer active — drop it instead of showing stale findings.
          if (this.setupMethodControl.value !== 'manual') {
            this.clearFhirPathFindings();
            return;
          }
          if (result) {
            this.fhirPathErrors = result.errors ?? [];
            this.fhirPathWarnings = result.warnings ?? [];
          }
          // Findings settled — refresh the Save button (enable if clean, keep disabled on errors).
          this.emitValidity();
        })
    );

    // Custom FHIRPath typing (the control only emits while enabled, i.e. in manual mode).
    this.subscriptions.add(
      this.fhirPathControl.valueChanges.subscribe(value => this.queueFhirPathValidation(value))
    );
  }

  /**
   * Queues the expression for validation. Only Custom (manual) FHIRPath is checked —
   * builder methods generate fixed templates. No-op in view-only mode.
   */
  private queueFhirPathValidation(expr: string | null | undefined): void {
    if (this.viewOnly || this.setupMethodControl.value !== 'manual') return;
    this.fhirPathValidation$.next(expr ?? '');
  }

  /** Drops any displayed findings; called when the expression they describe is no longer active. */
  private clearFhirPathFindings(): void {
    this.fhirPathErrors = [];
    this.fhirPathWarnings = [];
    this.validatingFhirPath = false;
    this.emitValidity();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['item'] || changes['vendor'] || changes['formMode']) {
      this.buildForm();
    }
  }

  private buildForm(): void {
    this.clearBuilderSubscriptions();
    this.configForm.reset();
    this.conditionsArray.clear();
    this.methodEntriesCache = {};
    this.manualCache = '';

    // Derive the single config-level method from the saved expression, then build
    // each OR operand as an entry rendered for that method.
    const method = this.deriveSetupMethod(this.item?.conditions);
    this.setupMethodControl.setValue(method, { emitEvent: false });
    this.previousMethod = method;

    // Flag (advisory) when the saved rules were built for a different vendor.
    this.vendorMismatch = this.detectVendorMismatch(this.item?.conditions);

    if (this.item) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();
      this.descriptionControl.setValue(this.item.description || '');
      this.descriptionControl.updateValueAndValidity();
      this.isActiveControl.setValue(this.item.isActive ?? true);
      this.isActiveControl.updateValueAndValidity();

      if (method === 'manual') {
        // Expression the builder can't represent → Custom FHIRPath. Keep one empty
        // builder row so switching to a builder method has something to start from.
        this.conditionsArray.push(this.createConditionGroup());
        this.fhirPathControl.setValue(this.rawCombined(this.item.conditions), { emitEvent: false });
      } else {
        // Expand each OR operand into a builder match rendered for the derived method.
        const operands = (this.item.conditions ?? [])
          .flatMap(cond => ReportingOrganizationConfigFormComponent.splitTopLevelOr(cond.fhirPath));
        if (operands.length > 0) {
          operands.forEach(expr => this.conditionsArray.push(this.createConditionGroup(expr)));
        } else {
          this.conditionsArray.push(this.createConditionGroup());
        }
        this.recomputeCombined();
      }
    } else {
      this.isActiveControl.setValue(true);
      this.isActiveControl.updateValueAndValidity();
      this.conditionsArray.push(this.createConditionGroup());
      this.recomputeCombined();
      this.formMode = this.formMode ?? FormMode.Create;
    }

    this.wireSetupMethod();
    this.toggleViewOnly(this.viewOnly);

    // Load the facility's frequency plans so activation can be pre-validated against the
    // Encounter/Location requirement before the user toggles Active on and saves.
    if (!this.viewOnly) {
      this.loadFacilityQueryPlans();
    }

    // Validate the expression a Custom-mode config loaded with (the value is set with
    // emitEvent: false, so nothing else triggers it). No-op for builder methods.
    this.queueFhirPathValidation(this.fhirPathControl.value);
  }

  /**
   * Picks the config-level method from the stored expression. Returns a builder method
   * (identifier / managingOrg / locationType) only when every OR operand reverse-parses
   * to the same vendor-appropriate method; otherwise 'manual' (Custom FHIRPath).
   */
  private deriveSetupMethod(conditions?: { fhirPath: string }[]): string {
    const operands = (conditions ?? [])
      .flatMap(cond => ReportingOrganizationConfigFormComponent.splitTopLevelOr(cond.fhirPath));
    if (operands.length === 0) {
      return this.defaultSetupMethod();
    }

    const first = this.parseFhirPath(operands[0]);
    const candidate = first?.setupMethod ?? null;
    const validForVendor =
      (this.isEpic && candidate === 'identifier') ||
      (this.isCerner && (candidate === 'managingOrg' || candidate === 'locationType'));
    if (!candidate || !validForVendor) {
      return 'manual';
    }

    const allMatch = operands.every(expr => {
      const parsed = this.parseFhirPath(expr);
      return parsed !== null && parsed.setupMethod === candidate;
    });
    return allMatch ? candidate : 'manual';
  }

  /** Flags a saved config whose rules reverse-parse to the other vendor's builder method.
   *  Custom/unparseable expressions aren't treated as a mismatch. */
  private detectVendorMismatch(conditions?: { fhirPath: string }[]): boolean {
    const operands = (conditions ?? [])
      .flatMap(cond => ReportingOrganizationConfigFormComponent.splitTopLevelOr(cond.fhirPath));
    return operands.some(expr => {
      const parsed = this.parseFhirPath(expr);
      if (!parsed) return false;
      const belongsToCurrentVendor =
        (this.isEpic && parsed.setupMethod === 'identifier') ||
        (this.isCerner && (parsed.setupMethod === 'managingOrg' || parsed.setupMethod === 'locationType'));
      return !belongsToCurrentVendor;
    });
  }

  /** Joins stored condition rows into a single expression (for the Custom FHIRPath field). */
  private rawCombined(conditions?: { fhirPath: string }[]): string {
    const raw = (conditions ?? [])
      .map(c => c.fhirPath)
      .filter(p => p && p.trim().length > 0)
      .map(p => p.trim());

    if (raw.length <= 1) {
      return raw[0] ?? '';
    }
    return raw.map(p => `(${p})`).join(' or ');
  }

  /**
   * On method change: switching to 'manual' seeds the editable field with whatever the
   * builder produced; switching to a builder method resets to one fresh match. Either
   * way, flip which controls are enabled.
   */
  private wireSetupMethod(): void {
    const sub = this.setupMethodControl.valueChanges.subscribe((method: string) => {
      if (this.viewOnly) return;

      // Findings describe the outgoing method's expression; clear them on any method change.
      // Validation re-runs only when the user edits a Custom expression.
      this.clearFhirPathFindings();

      // Snapshot the outgoing method's state so it can be restored if you switch back.
      if (this.previousMethod === 'manual') {
        this.manualCache = this.fhirPathControl.value ?? '';
      } else if (this.previousMethod) {
        this.methodEntriesCache[this.previousMethod] =
          this.conditionsArray.controls.map(ctrl => (ctrl as FormGroup).getRawValue());
      }

      // Restore the incoming method's previously-entered state, or start fresh.
      if (method === 'manual') {
        this.fhirPathControl.setValue(this.manualCache || this.buildCombinedFromEntries(), { emitEvent: false });
      } else {
        this.conditionsArray.clear();
        const cached = this.methodEntriesCache[method];
        if (cached && cached.length > 0) {
          cached.forEach(values => {
            const group = this.createConditionGroup();
            group.patchValue(values, { emitEvent: false });
            this.conditionsArray.push(group);
          });
        } else {
          this.conditionsArray.push(this.createConditionGroup());
        }
      }

      this.previousMethod = method;
      this.applyMethodState(method);
    });
    this.builderSubscriptions.push(sub);
  }

  /**
   * Enables the match builder for builder methods (FHIRPath becomes a read-only preview)
   * or the editable FHIRPath for 'manual' (builder disabled).
   */
  private applyMethodState(method: string): void {
    if (method === 'manual') {
      this.conditionsArray.disable({ emitEvent: false });
      this.fhirPathControl.enable({ emitEvent: false });
    } else {
      this.conditionsArray.enable({ emitEvent: false });
      this.fhirPathControl.disable({ emitEvent: false });
      this.applyAllConditionValidators(method);
      this.recomputeCombined();
    }
    // Recompute validity and emit so the Save button reflects the new method's required fields.
    this.configForm.updateValueAndValidity();
  }

  // ---- Getters ----

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get descriptionControl(): FormControl {
    return this.configForm.get('description') as FormControl;
  }

  get isActiveControl(): FormControl {
    return this.configForm.get('isActive') as FormControl;
  }

  get conditionsArray(): FormArray {
    return this.configForm.get('conditions') as FormArray;
  }

  get setupMethodControl(): FormControl {
    return this.configForm.get('setupMethod') as FormControl;
  }

  get fhirPathControl(): FormControl {
    return this.configForm.get('fhirPath') as FormControl;
  }

  get isEpic(): boolean {
    return this.vendor?.name?.trim()?.toLowerCase() === 'epic';
  }

  get isCerner(): boolean {
    return this.vendor?.name?.trim()?.toLowerCase() === 'cerner';
  }

  // ---- Condition group creation / parsing ----

  /**
   * Creates a match FormGroup (builder fields only). If an existing operand is provided
   * and reverse-parses, its values populate the builder fields. The single FHIRPath and
   * manual-edit toggle live at the config level, not per match.
   */
  createConditionGroup(fhirPath?: string): FormGroup {
    const parsed = fhirPath ? this.parseFhirPath(fhirPath) : null;

    const group = this.fb.group({
      // Epic identifier fields:
      identifierSystem: [''],
      identifierCode: [''],
      // Cerner managing org:
      organizationId: [''],
      // Cerner location type:
      locationTypeCode: ['783'],
      locationAlias: ['']
    });

    if (parsed) {
      group.patchValue({
        identifierSystem: parsed.identifierSystem ?? '',
        identifierCode: parsed.identifierCode ?? '',
        organizationId: parsed.organizationId ?? '',
        locationTypeCode: parsed.locationTypeCode ?? '783',
        locationAlias: parsed.locationAlias ?? ''
      });
    }

    this.applyConditionValidators(group, this.setupMethodControl.value);
    this.wireConditionGroup(group);
    return group;
  }

  /**
   * Sets `required` on exactly the builder fields rendered for the given method and clears
   * it from the rest, so a match can't be saved with any of its displayed fields empty
   * while hidden fields never block Save.
   */
  private applyConditionValidators(group: FormGroup, method: string): void {
    const required = ReportingOrganizationConfigFormComponent.RequiredFieldsByMethod[method] ?? [];
    ReportingOrganizationConfigFormComponent.BuilderFields.forEach(field => {
      const control = group.get(field);
      if (!control) return;
      if (required.includes(field)) {
        control.setValidators(ReportingOrganizationConfigFormComponent.requiredNonBlank);
      } else {
        control.clearValidators();
      }
      control.updateValueAndValidity({ emitEvent: false });
    });
  }

  /** Re-applies the active method's required-field rules to every existing match. */
  private applyAllConditionValidators(method: string): void {
    this.conditionsArray.controls.forEach(ctrl =>
      this.applyConditionValidators(ctrl as FormGroup, method)
    );
  }

  private defaultSetupMethod(): string {
    if (this.isCerner) {
      return 'managingOrg';
    }

    return this.isEpic ? 'identifier' : 'manual';
  }

  /**
   * Subscribes to a match's builder fields so the single combined FHIRPath preview
   * stays in sync while in builder mode.
   */
  private wireConditionGroup(group: FormGroup): void {
    const builderFields = [
      'identifierSystem',
      'identifierCode',
      'organizationId',
      'locationTypeCode',
      'locationAlias'
    ];

    builderFields.forEach(field => {
      const sub = group.get(field)!.valueChanges.subscribe(() => {
        if (!this.viewOnly) {
          this.recomputeCombined();
        }
      });
      this.builderSubscriptions.push(sub);
    });
  }

  /**
   * Rebuilds the single combined FHIRPath from the match entries. Each match is an OR
   * operand; a Location match may itself be `code and alias`.
   */
  private recomputeCombined(): void {
    // In Custom FHIRPath mode the typed value is the source of truth — never overwrite it.
    if (this.setupMethodControl.value === 'manual') return;
    this.fhirPathControl.setValue(this.buildCombinedFromEntries(), { emitEvent: false });
    this.fhirPathControl.updateValueAndValidity({ emitEvent: false });
  }

  private buildCombinedFromEntries(): string {
    const paths = this.conditionsArray.controls
      .map(ctrl => this.buildFhirPath(ctrl as FormGroup))
      .filter(p => p && p.trim().length > 0)
      .map(p => p.trim());

    if (paths.length <= 1) {
      return paths[0] ?? '';
    }
    return paths.map(p => `(${p})`).join(' or ');
  }

  private clearBuilderSubscriptions(): void {
    this.builderSubscriptions.forEach(sub => sub.unsubscribe());
    this.builderSubscriptions = [];
  }

  // ---- FHIRPath generation ----

  /**
   * Escapes a value for safe embedding inside a single-quoted FHIRPath string.
   */
  private static escape(value: string): string {
    return (value ?? '')
      .replace(/\\/g, '\\\\')
      .replace(/'/g, "\\'");
  }

  /**
   * Splits a combined boolean FHIRPath back into its top-level OR operands, ignoring
   * ' or ' that appears inside parentheses or quoted strings, and strips the wrapping
   * parens from each operand. A non-combined expression returns a single element.
   */
  private static splitTopLevelOr(path: string): string[] {
    if (!path) return [];
    const parts: string[] = [];
    let depth = 0;
    let inQuote = false;
    let current = '';

    for (let i = 0; i < path.length; i++) {
      const ch = path[i];
      if (ch === "'" && path[i - 1] !== '\\') {
        inQuote = !inQuote;
      }
      if (!inQuote && depth === 0 && path.substring(i, i + 4).toLowerCase() === ' or ') {
        parts.push(current);
        current = '';
        i += 3; // skip ' or'; the loop's i++ skips the trailing space
        continue;
      }
      if (!inQuote) {
        if (ch === '(') depth++;
        else if (ch === ')') depth--;
      }
      current += ch;
    }
    if (current.trim().length > 0) parts.push(current);

    return parts
      .map(p => ReportingOrganizationConfigFormComponent.stripOuterParens(p))
      .filter(p => p.length > 0);
  }

  /**
   * Removes a single layer of wrapping parentheses (repeatedly) when they enclose the
   * entire expression, leaving inner/grouping parens intact. Quote-aware.
   */
  private static stripOuterParens(value: string): string {
    let s = (value ?? '').trim();
    while (s.length >= 2 && s.startsWith('(') && s.endsWith(')')) {
      let depth = 0;
      let inQuote = false;
      let wraps = true;
      for (let i = 0; i < s.length; i++) {
        const ch = s[i];
        if (ch === "'" && s[i - 1] !== '\\') {
          inQuote = !inQuote;
        }
        if (inQuote) continue;
        if (ch === '(') depth++;
        else if (ch === ')') {
          depth--;
          if (depth === 0 && i !== s.length - 1) {
            wraps = false;
            break;
          }
        }
      }
      if (wraps) s = s.slice(1, -1).trim();
      else break;
    }
    return s;
  }

  buildFhirPath(group: FormGroup): string {
    const setupMethod = this.setupMethodControl.value;
    const esc = ReportingOrganizationConfigFormComponent.escape;

    if (setupMethod === 'identifier') {
      const system = esc(group.get('identifierSystem')!.value);
      const code = esc(group.get('identifierCode')!.value);
      return `Location.identifier.exists(system = '${system}' and value = '${code}')`;
    }

    if (setupMethod === 'managingOrg') {
      const orgId = esc(group.get('organizationId')!.value);
      return `Location.managingOrganization.reference = 'Organization/${orgId}'`;
    }

    if (setupMethod === 'locationType') {
      const typeCode = esc(group.get('locationTypeCode')!.value);
      const alias = group.get('locationAlias')!.value as string;
      let path = `Location.type.coding.exists(code = '${typeCode}')`;
      if (alias && alias.trim().length > 0) {
        path += ` and Location.alias = '${esc(alias)}'`;
      }
      return path;
    }

    return '';
  }

  /**
   * Best-effort reverse-match of a generated FHIRPath template back into
   * builder field values. Returns null for arbitrary hand-written paths.
   */
  parseFhirPath(path: string): {
    setupMethod: string;
    identifierSystem?: string;
    identifierCode?: string;
    organizationId?: string;
    locationTypeCode?: string;
    locationAlias?: string;
  } | null {
    if (!path) return null;
    const trimmed = path.trim();

    // Accept both the current `exists(...)` form and the legacy `where(...)` form so
    // configurations saved before the switch to boolean FHIRPath still reverse-parse.
    const identifierMatch = trimmed.match(
      /^Location\.identifier\.(?:exists|where)\(system = '(.*?)' and value = '(.*?)'\)$/
    );
    if (identifierMatch) {
      return {
        setupMethod: 'identifier',
        identifierSystem: identifierMatch[1].replace(/\\'/g, "'"),
        identifierCode: identifierMatch[2].replace(/\\'/g, "'")
      };
    }

    const managingOrgMatch = trimmed.match(
      /^Location\.managingOrganization\.reference = 'Organization\/(.*?)'$/
    );
    if (managingOrgMatch) {
      return {
        setupMethod: 'managingOrg',
        organizationId: managingOrgMatch[1].replace(/\\'/g, "'")
      };
    }

    const locationTypeWithAlias = trimmed.match(
      /^Location\.type\.coding\.(?:exists|where)\(code = '(.*?)'\) and Location\.alias = '(.*?)'$/
    );
    if (locationTypeWithAlias) {
      return {
        setupMethod: 'locationType',
        locationTypeCode: locationTypeWithAlias[1].replace(/\\'/g, "'"),
        locationAlias: locationTypeWithAlias[2].replace(/\\'/g, "'")
      };
    }

    const locationTypeMatch = trimmed.match(/^Location\.type\.coding\.(?:exists|where)\(code = '(.*?)'\)$/);
    if (locationTypeMatch) {
      return {
        setupMethod: 'locationType',
        locationTypeCode: locationTypeMatch[1].replace(/\\'/g, "'"),
        locationAlias: ''
      };
    }

    return null;
  }

  // ---- View-only handling ----

  toggleViewOnly(viewOnly: boolean): void {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.descriptionControl.disable();
      this.isActiveControl.disable();
      this.setupMethodControl.disable({ emitEvent: false });
      this.conditionsArray.disable({ emitEvent: false });
      this.fhirPathControl.disable({ emitEvent: false });
    } else {
      this.descriptionControl.enable();
      this.isActiveControl.enable();
      this.setupMethodControl.enable({ emitEvent: false });
      // Enable the builder or the editable FHIRPath depending on the current method.
      this.applyMethodState(this.setupMethodControl.value);
    }
  }

  // ---- Add / remove conditions ----

  addMatch(): void {
    this.conditionsArray.push(this.createConditionGroup());
    this.recomputeCombined();
  }

  removeMatch(index: number): void {
    if (this.conditionsArray.length <= 1) {
      return;
    }
    this.conditionsArray.removeAt(index);
    this.recomputeCombined();
  }

  // ---- Submit ----

  submitConfiguration(): void {
    this.saveError = null;

    // Don't submit an expression we already know is invalid; warnings are advisory and don't block.
    if (this.fhirPathErrors.length > 0) {
      this.saveError = 'Resolve the FHIRPath errors before saving.';
      return;
    }

    // Block activation client-side when a frequency plan is missing Encounter/Location, mirroring
    // the backend rule (which still enforces it if this check was bypassed). The inline message
    // above Save already explains the requirement, so just stop here.
    if (this.activationPrerequisiteError) {
      return;
    }

    if (this.configForm.valid) {
      // Persist the whole configuration as the single combined boolean FHIRPath
      // (one condition row). Compute it directly so it never depends on the disabled
      // builder-mode preview control's state. No schema change.
      const combined = (this.setupMethodControl.value === 'manual'
        ? (this.fhirPathControl.value ?? '')
        : this.buildCombinedFromEntries()).trim();
      const conditions: ICreateOrganizationLocationConditionModel[] = [
        { fhirPath: combined, priority: 1 }
      ];

      // Decide insert-vs-update by whether we already have a persisted config id — NOT by
      // formMode. A facility is meant to have a single reporting-organization config, and
      // formMode can be stale (opened in Create while a config already exists), which would
      // POST a duplicate row. A present configId unambiguously means "update the existing row".
      const existingConfigId = this.item?.configId;

      if (existingConfigId == null) {
        const payload: ICreateOrganizationLocationConfigurationModel = {
          description: this.descriptionControl.value || undefined,
          isActive: this.isActiveControl.value,
          conditions
        };

        this.dataAcquisitionService
          .createLocationConfiguration(this.facilityIdControl.value, payload)
          .subscribe({
            next: response => {
              this.submittedConfiguration.emit({
                id: String(response.configId ?? ''),
                message: 'Reporting Organization Configuration Created'
              });
            },
            error: err => this.setSaveError(err)
          });
      } else {
        const payload: IUpdateOrganizationLocationConfigurationModel = {
          description: this.descriptionControl.value || undefined,
          isActive: this.isActiveControl.value,
          conditions
        };

        this.dataAcquisitionService
          .updateLocationConfiguration(existingConfigId, payload)
          .subscribe({
            next: response => {
              this.submittedConfiguration.emit({
                id: String(response.configId ?? existingConfigId ?? ''),
                message: 'Reporting Organization Configuration Updated'
              });
            },
            error: err => this.setSaveError(err)
          });
      }
    } else {
      this.saveError = 'Please complete the required fields before saving.';
    }
  }

  private setSaveError(err: any): void {
    // The backend returns RFC ProblemDetails, so the message is in err.error.detail
    // (e.g. "Invalid FHIRPath syntax: ..."). Fall back to title, a raw string body, or a default.
    const problem = err?.error;
    this.saveError =
      (typeof problem === 'string' ? problem : (problem?.detail ?? problem?.title))
      || 'Failed to save Reporting Organization configuration';
  }
}
