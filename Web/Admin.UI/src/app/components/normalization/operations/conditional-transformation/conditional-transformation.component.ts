import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {FormMode} from "../../../../models/FormMode.enum";
import {IEntityCreatedResponse} from "../../../../interfaces/entity-created-response.model";
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatCardContent} from "@angular/material/card";
import {MatIcon} from "@angular/material/icon";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {MatButton, MatIconButton} from "@angular/material/button";
import {Observable, Subject, takeUntil} from "rxjs";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";
import {AtLeastOneConditionValidator} from "../validators/AtLeastOneConditionValidator";
import {MatInput} from "@angular/material/input";
import {MatFormField, MatLabel, MatError, MatSuffix} from "@angular/material/form-field";

import {
  ConditionalTransformOperation,
  Operator
} from "../../../../interfaces/normalization/conditional-transformation-operation-interface";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {MatTooltip} from "@angular/material/tooltip";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";

@Component({
  selector: 'app-conditional-transformation',
  imports: [
    MatFormField,
    ReactiveFormsModule,
    MatIcon,
    MatCardContent,
    MatLabel,
    MatError,
    MatOption,
    MatSelect,
    NgForOf,
    NgIf,
    MatIconButton,
    MatInput,
    MatSuffix,
    MatButton,
    MatTooltip
  ],
  templateUrl: './conditional-transformation.component.html',
  styleUrl: './conditional-transformation.component.scss'
})
export class ConditionalTransformationComponent implements OnInit, OnDestroy {

  form!: FormGroup;

  resourceTypes: string[] = [];

  @Input() operation!: IOperationModel;

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

  destroy$ = new Subject<void>()

  operationType: OperationType = OperationType.ConditionalTransform;

  vendors: IVendor[] = [];

  operatorList = Object.entries(Operator)
    .filter(([key, value]) => typeof value === 'number') // filter out reverse mappings
    .map(([key, value]) => ({
      value: value as number,
      label: this.formatEnumKey(key)
    }));

  formatEnumKey(key: string): string {
    return key.replace(/([a-z])([A-Z])/g, '$1 $2');
  }


  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      facilityId: new FormControl({value: '', disabled: true}, Validators.required),
      description: new FormControl('', Validators.required),
      name: new FormControl('', Validators.required),
      targetFhirPath: new FormControl('', Validators.required),
      targetValue: new FormControl('', Validators.required),
      conditions: this.fb.array([], AtLeastOneConditionValidator),
      selectedVendor: new FormControl('')
    }, {validators: facilityOrVendorRequiredValidator});
  }

  ngOnInit() {

    // load resource types from api
    this.getResourceTypes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: types => (this.resourceTypes = types),
        error: () =>
          this.snackBar.open('Failed to load resource types', '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          })
      });

    this.operationService.getVendors().subscribe({
      next: (data) => {
        this.vendors = data;
        if (this.formMode === FormMode.Edit) {
          if (this.isVendorMode && Array.isArray(this.operation.vendorPresets)) {
            const matchedVendorIds: string[] = [];

            for (const preset of this.operation.vendorPresets) {
              const vendorName = preset.vendorVersion?.vendor?.name;

              if (vendorName) {
                const match = this.vendors.find(v => v.name === vendorName);
                if (match) {
                  matchedVendorIds.push(match.id);
                }
              }
            }

            if (matchedVendorIds.length > 0) {
              this.selectedVendorControl.setValue(matchedVendorIds);
            }
          }
        }
      },
      error: (err) => console.error('Error loading vendors', err)
    });

    // Add the initial condition row
    this.addCondition();

    // React to value changes if needed
    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.formValueChanged.emit(this.form.invalid);
    });

    // set the facilityId regardless of Edit/Add
    this.facilityIdControl.setValue(this.operation.facilityId);
    this.facilityIdControl.updateValueAndValidity();

    if (this.formMode === FormMode.Edit) {

      const conditionalTransformOperation = this.operation.parsedOperationJson as ConditionalTransformOperation;
      this.descriptionControl.setValue(this.operation.description);
      this.descriptionControl.updateValueAndValidity();

      this.nameControl.setValue(conditionalTransformOperation?.Name);
      this.nameControl.updateValueAndValidity();

      // get resource types
      this.selectedResourceTypesControl.setValue(
        [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
      );
      this.selectedResourceTypesControl.updateValueAndValidity();

      this.targetFhirPathControl.setValue(conditionalTransformOperation?.TargetFhirPath);
      this.targetFhirPathControl.updateValueAndValidity();

      this.targetValueControl.setValue(conditionalTransformOperation?.TargetValue);
      this.targetValueControl.updateValueAndValidity();

      // how to populate conditions based on OperationJson
      if (Object.keys(conditionalTransformOperation).length !== 0) {
        this.populateConditionsFromOperation(conditionalTransformOperation);
      }

    }
  }

  getResourceTypes(): Observable<string[]> {
    return this.operationService.getResourceTypes();
  }

  get facilityIdControl(): FormControl {
    return this.form.get('facilityId') as FormControl;
  }

  get selectedResourceTypesControl(): FormControl {
    return this.form.get('selectedResourceTypes') as FormControl;
  }

  get nameControl(): FormControl {
    return this.form.get('name') as FormControl;
  }

  get descriptionControl(): FormControl {
    return this.form.get('description') as FormControl;
  }

  get conditions(): FormArray {
    return this.form.get('conditions') as FormArray;
  }

  get targetFhirPathControl(): FormControl {
    return this.form.get('targetFhirPath') as FormControl;
  }

  get targetValueControl(): FormControl {
    return this.form.get('targetValue') as FormControl;
  }

  get selectedVendorControl(): FormControl {
    return this.form.get('selectedVendor') as FormControl;
  }


  clearName(): void {
    this.nameControl.setValue('');
    this.nameControl.updateValueAndValidity();
  }

  clearDescription(): void {
    this.descriptionControl.setValue('');
    this.descriptionControl.updateValueAndValidity();
  }

  clearTargetFhirPath(): void {
    this.targetFhirPathControl.setValue('');
    this.targetFhirPathControl.updateValueAndValidity();
  }

  clearTargetValue(): void {
    this.targetValueControl.setValue('');
    this.targetValueControl.updateValueAndValidity();
  }

  addCondition(): void {
    this.conditions.push(this.fb.group({
      fhir_Path_Source: ['', Validators.required],
      operator: ['', Validators.required],
      value: [''] // Optional, no validators
    }));
  }

  get isVendorMode(): boolean {
    return !this.operation.facilityId;
  }

  removeCondition(i: number) {
    this.conditions.removeAt(i);
  }

  compareResourceTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  populateConditionsFromOperation(operationJson: any) {
    if (!operationJson) return;
    const conditionsArray = this.form.get('conditions') as FormArray;
    conditionsArray.clear();
    if (operationJson.Conditions?.length) {
      operationJson.Conditions.forEach((cond: any) => {
        const conditionGroup = this.fb.group({
          fhir_Path_Source: [cond.Fhir_Path_Source || '', Validators.required],
          operator: [cond.Operator ?? null, Validators.required],
          value: [cond.Value || '']
        });

        conditionsArray.push(conditionGroup);
      });
    }
  }

  submitConfiguration(): void {
    this.form.markAllAsTouched();

    if (this.form.valid) {
      const operationJsonObj = {
        OperationType: OperationType.ConditionalTransform.toString(),
        Name: this.form.get('name')?.value,
        Description: this.form.get('description')?.value,
        TargetFhirPath: this.form.get('targetFhirPath')?.value,
        TargetValue: this.form.get('targetValue')?.value,
        Conditions: this.conditions.value
      };

      if (this.formMode == FormMode.Create) {
        this.operationService.createOperationConfiguration({
          resourceTypes: this.selectedResourceTypesControl.value,
          facilityId: this.operation.facilityId,
          description: this.descriptionControl.value,
          operationType: this.operationType,
          operation: operationJsonObj,
          vendorIds: this.selectedVendorControl?.value ? this.selectedVendorControl?.value : []
        } as ISaveOperationModel).subscribe({
          next: (response) => {
            this.submittedConfiguration.emit({id: '', message: `Operation created successfully.`});
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: '', message: `Error creating operation.`});
          }
        });
      } else if (this.formMode == FormMode.Edit) {
        this.operationService.updateOperationConfiguration({
          id: this.operation.id,
          resourceTypes: this.selectedResourceTypesControl.value,
          facilityId: this.operation.facilityId,
          description: this.descriptionControl.value,
          operationType: this.operationType,
          operation: operationJsonObj,
          vendorIds: this.selectedVendorControl?.value ? this.selectedVendorControl?.value : []
        } as ISaveOperationModel).subscribe({
          next: (response) => {
            this.submittedConfiguration.emit({id: '', message: `Operation updated successfully.`});
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: '', message: `Error updating operation.`});
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
