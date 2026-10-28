import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {AbstractControl, FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators} from '@angular/forms';
import {
  ILiteralQueryParameterModel,
  IParameterQueryConfigModel,
  IReferenceQueryConfigModel,
  IResourceIdsParameterModel,
  IVariableParameterModel,
  QueryConfigModel,
  QueryParameterModel,
  ReferenceQueryOperationType,
  VariableParameterType
} from '../../../interfaces/data-acquisition/query-plan-model.interface';
import {CommonModule} from '@angular/common';
import {MatButtonModule} from '@angular/material/button';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatIconModule} from '@angular/material/icon';
import {MatTableModule} from '@angular/material/table';

@Component({
  selector: 'app-query-config-edit',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatTableModule
  ],
  templateUrl: './query-config-edit.component.html',
  styleUrls: ['./query-config-edit.component.scss']
})
export class QueryConfigEditComponent implements OnInit {
  @Input() config!: QueryConfigModel;
  @Input() viewOnly: boolean = false;
  @Output() configChanged = new EventEmitter<QueryConfigModel>();

  queryForm!: FormGroup;
  resourceTypes = [
    'AllergyIntolerance', 'Condition', 'Coverage', 'Device', 'DiagnosticReport',
    'Encounter', 'Location', 'Medication', 'MedicationAdministration',
    'MedicationRequest', 'MedicationStatement', 'Observation', 'Patient',
    'Procedure', 'ServiceRequest', 'Specimen'
  ].sort();

  queryConfigTypes = ['Parameter', 'Reference'];
  operationTypes = [
    { value: ReferenceQueryOperationType.read, label: 'Read' },
    { value: ReferenceQueryOperationType.search, label: 'Search' },
    { value: ReferenceQueryOperationType.searchPost, label: 'SearchPost' }
  ];

  parameterTypes = ['Variable', 'Literal', 'ResourceIds'];
  variableTypes = [
    { value: VariableParameterType.patient, label: 'Patient' },
    { value: VariableParameterType.lookbackStart, label: 'Lookback Start' },
    { value: VariableParameterType.periodStart, label: 'Period Start' },
    { value: VariableParameterType.periodEnd, label: 'Period End' }
  ];

  constructor(private fb: FormBuilder) {}

  private normalizeOperationType(value: unknown): ReferenceQueryOperationType {
    if (value === null || value === undefined) {
      return ReferenceQueryOperationType.search;
    }

    if (typeof value === 'number') {
      return value as ReferenceQueryOperationType;
    }

    if (typeof value === 'string') {
      const sanitized = value.trim();
      if (!sanitized) {
        return ReferenceQueryOperationType.search;
      }

      const numericValue = Number(sanitized);
      if (!Number.isNaN(numericValue)) {
        return numericValue as ReferenceQueryOperationType;
      }

      const key = sanitized.toLowerCase().replace(/[^a-z]/g, '');
      const map: Record<string, ReferenceQueryOperationType> = {
        read: ReferenceQueryOperationType.read,
        search: ReferenceQueryOperationType.search,
        searchpost: ReferenceQueryOperationType.searchPost
      };
      if (key in map) {
        return map[key];
      }
    }

    return ReferenceQueryOperationType.search;
  }

  ngOnInit(): void {
    this.initForm();
  }

  initForm(): void {
    this.queryForm = this.fb.group({
      resourceType: [this.config?.resourceType || '', Validators.required],
      queryConfigType: [this.config?.queryConfigType || 'Parameter', Validators.required],
      // Reference specific
      operationType: [this.normalizeOperationType((this.config as any)?.operationType)],
      paged: [this.config?.queryConfigType === 'Reference' ? (this.config as IReferenceQueryConfigModel).paged : 100],
      // Parameter specific
      parameters: this.fb.array([])
    });

    if (this.config?.queryConfigType === 'Parameter') {
      const paramConfig = this.config as IParameterQueryConfigModel;
      paramConfig.parameters?.forEach(param => {
        this.addParameter(param);
      });
    }

    if (this.viewOnly) {
      this.queryForm.disable();
    }

    this.queryForm.get('queryConfigType')?.valueChanges.subscribe(type => {
      this.updateValidators(type);
    });
    this.updateValidators(this.queryForm.get('queryConfigType')?.value);
  }

  updateValidators(type: string): void {
    const operationTypeCtrl = this.queryForm.get('operationType');
    const pagedCtrl = this.queryForm.get('paged');
    const parametersCtrl = this.parameters;
    operationTypeCtrl?.setValidators(Validators.required);

    if (type === 'Reference') {
      pagedCtrl?.setValidators([Validators.required, Validators.min(1)]);
      // Reference queries don't use parameters.
      parametersCtrl.clearValidators();
    } else {
      pagedCtrl?.clearValidators();
      // A Parameter query must define at least one parameter (mirrors the
      // backend rule in ValidateQueryPlanConfigDictionaryAttribute).
      parametersCtrl.setValidators(this.minLengthArrayValidator(1));

      // Since parameter doesn't use read, reset read to search
      // which is the default for parameter
      if (operationTypeCtrl?.value === ReferenceQueryOperationType.read) {
        operationTypeCtrl.setValue(ReferenceQueryOperationType.search);
      }
    }
    operationTypeCtrl?.updateValueAndValidity();
    pagedCtrl?.updateValueAndValidity();
    parametersCtrl.updateValueAndValidity();
  }

  // Requires a FormArray to contain at least `min` entries.
  private minLengthArrayValidator(min: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const length = (control as FormArray).length;
      return length >= min ? null : { minLengthArray: { required: min, actual: length } };
    };
  }

  get parameters(): FormArray {
    return this.queryForm.get('parameters') as FormArray;
  }

  addParameter(param?: QueryParameterModel): void {
    const paramGroup = this.fb.group({
      parameterType: [param?.parameterType || 'Variable', Validators.required],
      name: [param?.name || '', Validators.required],
      // Variable
      variable: [param?.parameterType === 'Variable' ? this.normalizeVariableType((param as IVariableParameterModel).variable) : VariableParameterType.patient],
      format: [param?.parameterType === 'Variable' ? (param as IVariableParameterModel).format : ''],
      // Literal
      literal: [param?.parameterType === 'Literal' ? (param as ILiteralQueryParameterModel).literal : ''],
      // ResourceIds
      resource: [param?.parameterType === 'ResourceIds' ? (param as IResourceIdsParameterModel).resource : ''],
      paged: [param?.parameterType === 'ResourceIds' ? (param as IResourceIdsParameterModel).paged : '']
    });

    if (this.viewOnly) {
      paramGroup.disable();
    }

    this.parameters.push(paramGroup);
  }

  removeParameter(index: number): void {
    this.parameters.removeAt(index);
  }

  private normalizeVariableType(value: unknown): VariableParameterType {
    if (value === null || value === undefined) {
      return VariableParameterType.patient;
    }

    if (typeof value === 'number') {
      return value as VariableParameterType;
    }

    if (typeof value === 'string') {
      const sanitized = value.trim();
      if (!sanitized) {
        return VariableParameterType.patient;
      }

      const numericValue = Number(sanitized);
      if (!Number.isNaN(numericValue)) {
        return numericValue as VariableParameterType;
      }

      const key = sanitized.toLowerCase().replace(/[^a-z]/g, '');
      const map: Record<string, VariableParameterType> = {
        patient: VariableParameterType.patient,
        lookbackstart: VariableParameterType.lookbackStart,
        periodstart: VariableParameterType.periodStart,
        periodend: VariableParameterType.periodEnd
      };
      if (key in map) {
        return map[key];
      }
    }

    return VariableParameterType.patient;
  }

  save(): void {
    if (this.queryForm.invalid) {
      // Surface validation errors (incl. the "at least one parameter" rule)
      // that would otherwise stay hidden until a control is touched.
      this.queryForm.markAllAsTouched();
      return;
    }

    const formValue = this.queryForm.value;
    let result: QueryConfigModel;

    if (formValue.queryConfigType === 'Reference') {
      result = {
        resourceType: formValue.resourceType,
        queryConfigType: 'Reference',
        operationType: formValue.operationType,
        paged: formValue.paged
      } as IReferenceQueryConfigModel;
    } else {
      const params = this.parameters.controls.map(ctrl => {
        const val = ctrl.value;
        if (val.parameterType === 'Variable') {
          return {
            parameterType: 'Variable',
            name: val.name,
            variable: val.variable,
            format: val.format || null
          } as IVariableParameterModel;
        } else if (val.parameterType === 'Literal') {
          return {
            parameterType: 'Literal',
            name: val.name,
            literal: val.literal
          } as ILiteralQueryParameterModel;
        } else {
          return {
            parameterType: 'ResourceIds',
            name: val.name,
            resource: val.resource,
            paged: val.paged
          } as IResourceIdsParameterModel;
        }
      });

      result = {
        resourceType: formValue.resourceType,
        queryConfigType: 'Parameter',
        operationType: formValue.operationType,
        parameters: params
      } as IParameterQueryConfigModel;
    }

    this.configChanged.emit(result);
  }
}
