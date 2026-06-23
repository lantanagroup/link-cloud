import {Component, EventEmitter, Input, Output, SimpleChanges} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from "@angular/forms";
import {IQueryPlanModel, QueryConfigModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";
import {FormMode} from "../../../models/FormMode.enum";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";

import {MatButtonModule} from "@angular/material/button";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatInputModule} from "@angular/material/input";
import {MatIconModule} from "@angular/material/icon";
import {MatChipsModule} from "@angular/material/chips";
import {MatSelectModule} from "@angular/material/select";
import {MatSlideToggleModule} from "@angular/material/slide-toggle";
import {MatSnackBar, MatSnackBarModule} from "@angular/material/snack-bar";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatCardModule} from "@angular/material/card";
import {MatTabsModule} from "@angular/material/tabs";
import {MatExpansionModule} from "@angular/material/expansion";
import {MatProgressSpinnerModule} from "@angular/material/progress-spinner";
import {DataAcquisitionService} from "../../../services/gateway/data-acquisition/data-acquisition.service";
import {CdkDragDrop, DragDropModule, moveItemInArray} from '@angular/cdk/drag-drop';
import {MatDialog} from '@angular/material/dialog';
import {QueryConfigEditDialogComponent} from '../query-config-edit/query-config-edit-dialog.component';
import {MatTableModule} from '@angular/material/table';
import {CommonModule} from '@angular/common';

@Component({
  selector: 'app-query-plan-config-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    FormsModule,
    MatCardModule,
    MatTabsModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    DragDropModule,
    MatTableModule
],
  templateUrl: './query-plan-config.component.html',
  styleUrl: './query-plan-config.component.scss'
})
export class QueryPlanConfigFormComponent {

  @Input() item!: IQueryPlanModel;

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

  @Output() planSelected = new EventEmitter<any>();

  planForm: FormGroup;

  isInvalidJson = false;

  types = [
    { value: 'Discharge', label: 'Discharge' },
    { value: 'Weekly', label: 'Weekly' },
    { value: 'Daily', label: 'Daily' },
    { value: 'Monthly', label: 'Monthly' }
  ];

  initialQueries: QueryConfigModel[] = [];
  supplementalQueries: QueryConfigModel[] = [];
  displayedColumns: string[] = ['drag', 'resourceType', 'queryConfigType', 'details', 'actions'];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService, private fb: FormBuilder, private dialog: MatDialog) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.planForm = new FormGroup({
      planName: new FormControl('', Validators.required),
      facilityId: new FormControl('', Validators.required),
      ehrDescription: new FormControl('', Validators.required),
      lookBack: new FormControl('', Validators.required),
      type: new FormControl('', Validators.required)
    });
  }

  ngOnInit(): void {
    this.planForm.reset();

    if (this.item) {
      this.setFormValues();
    } else {
      this.formMode = FormMode.Create;
    }

    this.planForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['item'] && changes['item'].currentValue) {
      this.setFormValues();
    }
    // toggle view
    this.toggleViewOnly(this.viewOnly);
  }

  setFormValues() {
    this.planNameControl.setValue(this.item.planName);
    this.planNameControl.updateValueAndValidity();

    this.facilityIdControl.setValue(this.item.facilityId);
    this.facilityIdControl.updateValueAndValidity();

    this.typeControl.setValue(this.item.type);
    this.typeControl.updateValueAndValidity();

    this.ehrDescriptionControl.setValue(this.item.ehrDescription);
    this.ehrDescriptionControl.updateValueAndValidity();

    this.lookBackControl.setValue(this.item.lookBack);
    this.lookBackControl.updateValueAndValidity();

    this.initialQueries = this.recordToArray(this.item.initialQueries);
    this.supplementalQueries = this.recordToArray(this.item.supplementalQueries);
  }

  recordToArray(record: Record<string, QueryConfigModel> | undefined | string): QueryConfigModel[] {
    if (!record) return [];
    if (typeof record === 'string') {
      try {
        record = JSON.parse(record);
      } catch {
        return [];
      }
    }
    const entries = Object.entries(record as Record<string, QueryConfigModel>);
    return entries
      .sort((a, b) => parseInt(a[0]) - parseInt(b[0]))
      .map(e => this.normalizeQueryConfig(e[1]));
  }

  arrayToRecord(array: QueryConfigModel[]): Record<string, QueryConfigModel> {
    const record: Record<string, QueryConfigModel> = {};
    array.forEach((item, index) => {
      const normalized = this.normalizeQueryConfig(item);
      record[index.toString()] = normalized;
    });
    return record;
  }

  dropInitial(event: CdkDragDrop<QueryConfigModel[]>) {
    if (this.viewOnly) return;
    const reordered = [...this.initialQueries];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    if (!this.isValidOrder(reordered)) {
      this.showOrderingError();
      return;
    }
    this.initialQueries = reordered;
    this.formValueChanged.emit(this.planForm.invalid);
  }

  dropSupplemental(event: CdkDragDrop<QueryConfigModel[]>) {
    if (this.viewOnly) return;
    const reordered = [...this.supplementalQueries];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    if (!this.isValidOrder(reordered)) {
      this.showOrderingError();
      return;
    }
    this.supplementalQueries = reordered;
    this.formValueChanged.emit(this.planForm.invalid);
  }

  // Mirrors the backend rule (QueryPlanValidator.ValidateQueryOrder): no
  // Parameter query may appear after a Reference query.
  private isValidOrder(queries: QueryConfigModel[]): boolean {
    let seenReference = false;
    for (const query of queries) {
      if (query.queryConfigType === 'Reference') {
        seenReference = true;
      } else if (seenReference) {
        return false;
      }
    }
    return true;
  }


  private normalizeOrder(queries: QueryConfigModel[]): QueryConfigModel[] {
    const parameters = queries.filter(q => q.queryConfigType !== 'Reference');
    const references = queries.filter(q => q.queryConfigType === 'Reference');
    return [...parameters, ...references];
  }

  private showOrderingError(): void {
    this.snackBar.open('Parameter queries must appear before Reference queries.', '', {
      duration: 3500,
      panelClass: 'error-snackbar',
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }

  addInitialQuery() {
    this.openQueryEditDialog(null, (res) => {
      this.initialQueries = this.insertRespectingOrder(this.initialQueries, res);
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }

  addSupplementalQuery() {
    this.openQueryEditDialog(null, (res) => {
      this.supplementalQueries = this.insertRespectingOrder(this.supplementalQueries, res);
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }


  private insertRespectingOrder(queries: QueryConfigModel[], res: QueryConfigModel): QueryConfigModel[] {
    if (res.queryConfigType === 'Reference') {
      return [...queries, res];
    }
    const firstReferenceIndex = queries.findIndex(q => q.queryConfigType === 'Reference');
    if (firstReferenceIndex === -1) {
      return [...queries, res];
    }
    return [
      ...queries.slice(0, firstReferenceIndex),
      res,
      ...queries.slice(firstReferenceIndex)
    ];
  }

  editInitialQuery(index: number) {
    this.openQueryEditDialog(this.initialQueries[index], (res) => {
      const updated = [...this.initialQueries];
      updated[index] = res;
      if (!this.isValidOrder(updated)) {
        this.showOrderingError();
        return;
      }
      this.initialQueries = updated;
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }

  editSupplementalQuery(index: number) {
    this.openQueryEditDialog(this.supplementalQueries[index], (res) => {
      const updated = [...this.supplementalQueries];
      updated[index] = res;
      if (!this.isValidOrder(updated)) {
        this.showOrderingError();
        return;
      }
      this.supplementalQueries = updated;
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }

  deleteInitialQuery(index: number) {
    this.initialQueries.splice(index, 1);
    this.initialQueries = [...this.initialQueries];
    this.formValueChanged.emit(this.planForm.invalid);
  }

  deleteSupplementalQuery(index: number) {
    this.supplementalQueries.splice(index, 1);
    this.supplementalQueries = [...this.supplementalQueries];
    this.formValueChanged.emit(this.planForm.invalid);
  }

  openQueryEditDialog(config: QueryConfigModel | null, callback: (res: QueryConfigModel) => void) {
    const dialogRef = this.dialog.open(QueryConfigEditDialogComponent, {
      width: '800px',
      data: {
        dialogTitle: config ? 'Edit Query' : 'Add Query',
        config: config ? JSON.parse(JSON.stringify(config)) : { resourceType: '', queryConfigType: 'Parameter', parameters: [] },
        viewOnly: this.viewOnly
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        callback(result);
      }
    });
  }

  // Method to get the label based on the value
  getLabelFromValue(value: string): string {
    const selected = this.types.find(type => type.value === value);
    return selected ? selected.label : 'No label found';
  }

  onTypeChange(event: any) {
    console.log('Selected type:', event.value);
    this.loadQueryPlan();
  }

  loadQueryPlan() {
      this.dataAcquisitionService.getQueryPlanConfiguration(this.facilityIdControl.value, this.typeControl.value).subscribe((data: IQueryPlanModel) => {
        this.item = data;
        this.planSelected.emit({"type" : this.typeControl.value, "label": this.getLabelFromValue(this.typeControl.value), "exists" : true});
      }, error => {
        if (error.status == 404) {
          this.snackBar.open(`No current Query plan found for facility ${this.facilityIdControl.value} and type ${this.getLabelFromValue(this.typeControl.value)}, please create one.`, '', {
            duration: 5000,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.item = {
            id: '',
            facilityId: this.facilityIdControl.value,
            planName: '',
            ehrDescription: '',
            lookBack: '',
            initialQueries: {},
            supplementalQueries: {},
            type: this.typeControl.value ?? "Discharge"
          } as IQueryPlanModel;
          this.planSelected.emit({"type" : this.typeControl.value, "label": this.getLabelFromValue(this.typeControl.value), "exists" : false});
        } else {
          this.snackBar.open(`Failed to load FHIR query plan for the facility, see error for details.`, '', {
            duration: 5000,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
  }

  // Getter methods for form controls
  get planNameControl(): FormControl {
    return this.planForm.get('planName') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.planForm.get('facilityId') as FormControl;
  }

  get ehrDescriptionControl(): FormControl {
    return this.planForm.get('ehrDescription') as FormControl;
  }

  get lookBackControl(): FormControl {
    return this.planForm.get('lookBack') as FormControl;
  }

  get typeControl(): FormControl {
    return this.planForm.get('type') as FormControl;
  }

  clearPlanName(): void {
    this.planNameControl.setValue('');
    this.planNameControl.updateValueAndValidity();
  }

  clearEhrDescription(): void {
    this.ehrDescriptionControl.setValue('');
    this.ehrDescriptionControl.updateValueAndValidity();
  }

  clearLookBack(): void {
    this.lookBackControl.setValue('');
    this.lookBackControl.updateValueAndValidity();
  }

  private normalizeOperationType(value: unknown): number {
    if (value === null || value === undefined) return 1; // default Search
    if (typeof value === 'number') return value;
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed) return 1;
      const numeric = Number(trimmed);
      if (!Number.isNaN(numeric)) return numeric;
      const key = trimmed.toLowerCase().replace(/[^a-z]/g, '');
      const map: Record<string, number> = { read: 0, search: 1, searchpost: 2 };
      if (key in map) return map[key];
    }
    return 1;
  }

  private normalizeVariableType(value: unknown): number {
    if (value === null || value === undefined) return 0; // patient
    if (typeof value === 'number') return value;
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (!trimmed) return 0;
      const numeric = Number(trimmed);
      if (!Number.isNaN(numeric)) return numeric;
      const key = trimmed.toLowerCase().replace(/[^a-z]/g, '');
      const map: Record<string, number> = {
        patient: 0,
        lookbackstart: 1,
        periodstart: 2,
        periodend: 3
      };
      if (key in map) return map[key];
    }
    return 0;
  }

  private normalizeQueryConfig(cfg: QueryConfigModel): QueryConfigModel {
    if (!cfg) return cfg;
    const c: any = cfg as any;

    c.operationType = this.normalizeOperationType(c.operationType);

    if (c.parameters && Array.isArray(c.parameters)) {
      c.parameters = c.parameters.map((p: any) => {
        if (p && p.parameterType === 'Variable') {
          p.variable = this.normalizeVariableType(p.variable);
        }
        return p;
      });
    }

    return c as QueryConfigModel;
  }

  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.planNameControl.disable();
      this.ehrDescriptionControl.disable();
      this.lookBackControl.disable();
      this.typeControl.enable();
    } else {
      this.planNameControl.enable();
      this.ehrDescriptionControl.enable();
      this.lookBackControl.enable();
      this.typeControl.disable();
    }
  }


  get currentPlanJson(): string {
    const plan: any = {
      Id: this.item?.id || '',
      PlanName: this.planNameControl.value,
      FacilityId: this.facilityIdControl.value,
      EHRDescription: this.ehrDescriptionControl.value,
      LookBack: this.lookBackControl.value,
      InitialQueries: this.arrayToRecord(this.normalizeOrder(this.initialQueries)),
      SupplementalQueries: this.arrayToRecord(this.normalizeOrder(this.supplementalQueries)),
      Type: this.typeControl.value
    };
    return JSON.stringify(plan, null, 2);
  }

  applyImportedPlan(plan: any, preserveFacilityId: boolean, preserveType: boolean): string | null {
    if (!plan || typeof plan !== 'object' || Array.isArray(plan)) {
      return 'Imported JSON must be an object.';
    }

    const incomingInitialQueries = plan.InitialQueries ?? plan.initialQueries ?? {};
    const incomingSupplementalQueries = plan.SupplementalQueries ?? plan.supplementalQueries ?? {};

    if (incomingInitialQueries && (typeof incomingInitialQueries !== 'object' || Array.isArray(incomingInitialQueries))) {
      return 'InitialQueries must be an object.';
    }

    if (incomingSupplementalQueries && (typeof incomingSupplementalQueries !== 'object' || Array.isArray(incomingSupplementalQueries))) {
      return 'SupplementalQueries must be an object.';
    }

    const facilityId = preserveFacilityId
      ? this.facilityIdControl.value
      : (plan.FacilityId ?? plan.facilityId ?? this.facilityIdControl.value ?? '');

    const type = preserveType
      ? this.typeControl.value
      : (plan.Type ?? plan.type ?? this.typeControl.value ?? 'Discharge');

    this.item = {
      id: plan.Id ?? plan.id ?? this.item?.id ?? '',
      planName: plan.PlanName ?? plan.planName ?? '',
      facilityId: facilityId ?? '',
      ehrDescription: plan.EHRDescription ?? plan.ehrDescription ?? '',
      lookBack: plan.LookBack ?? plan.lookBack ?? '',
      initialQueries: incomingInitialQueries ?? {},
      supplementalQueries: incomingSupplementalQueries ?? {},
      type: type ?? 'Discharge'
    } as IQueryPlanModel;

    this.setFormValues();
    this.formValueChanged.emit(this.planForm.invalid);

    return null;
  }

  submitConfiguration(): void {
    if (this.planForm.valid) {
        if (this.formMode == FormMode.Create) {
          this.dataAcquisitionService.createQueryPlanConfiguration(this.facilityIdControl.value, {
            PlanName: this.planNameControl.value,
            FacilityId: this.facilityIdControl.value,
            EHRDescription: this.ehrDescriptionControl.value,
            LookBack: this.lookBackControl.value,
            InitialQueries: this.arrayToRecord(this.normalizeOrder(this.initialQueries)),
            SupplementalQueries: this.arrayToRecord(this.normalizeOrder(this.supplementalQueries)),
            Type: this.typeControl.value
          } as any).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({id: '', message: `Created query plan`});
            },
            error: (err) => {
              this.submittedConfiguration.emit({id: '', message: `Error Creating plan`});
            }
          });
        } else if (this.formMode == FormMode.Edit) {
          this.dataAcquisitionService.updateQueryPlanConfiguration(this.facilityIdControl.value,
            {
              Id: this.item.id,
              PlanName: this.planNameControl.value,
              FacilityId: this.facilityIdControl.value,
              EHRDescription: this.ehrDescriptionControl.value,
              LookBack: this.lookBackControl.value,
              InitialQueries: this.arrayToRecord(this.initialQueries),
              SupplementalQueries: this.arrayToRecord(this.supplementalQueries),
              Type: this.typeControl.value
            } as any).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({id: '', message: `Updated query plan`});
            },
            error: (err) => {
              this.submittedConfiguration.emit({id: '', message: `Error updating query plan`});
            }
          });
        }
    } else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  getOperationTypeDisplay(op: number|string) {
    switch (op) {
      case 0:
      case '0':
      case 'Read':
        return 'Read';
      case 1:
      case '1':
      case 'Search':
        return 'Search';
      case 2:
      case '2':
      case 'SearchPost':
        return 'SearchPost';
      default:
        return 'Unknown';
    }
  }
}
