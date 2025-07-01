import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  FormArray,
  Validators,
  AbstractControl,
  FormsModule,
  ReactiveFormsModule
} from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import {Observable, Subject, takeUntil} from 'rxjs';

import { FormMode } from '../../../../models/FormMode.enum';
import { IEntityCreatedResponse } from '../../../../interfaces/entity-created-response.model';
import { IOperationModel } from '../../../../interfaces/normalization/operation-get-model.interface';
import { CodeMapOperation, CodeMap } from '../../../../interfaces/normalization/code-map-operation-interface';
import { OperationType } from '../../../../interfaces/normalization/operation-type-enumeration';
import { ISaveOperationModel } from '../../../../interfaces/normalization/operation-save-model.interface';
import { OperationService } from '../../../../services/gateway/normalization/operation.service';
import {MatIcon} from "@angular/material/icon";
import {MatButton, MatIconButton} from "@angular/material/button";
import {MatError, MatFormField, MatInput, MatLabel, MatSuffix} from "@angular/material/input";
import {MatCard, MatCardContent, MatCardHeader} from "@angular/material/card";
import {NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {AtLeastOneConditionValidator} from "../validators/AtLeastOneConditionValidator";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";

@Component({
  selector: 'app-code-map',
  templateUrl: './code-map.component.html',
  imports: [
    MatIcon,
    MatIconButton,
    MatButton,
    MatFormField,
    MatInput,
    MatFormField,
    FormsModule,
    MatCardContent,
    ReactiveFormsModule,
    MatSelect,
    MatOption,
    MatLabel,
    NgIf,
    NgForOf,
    MatCard,
    MatCardHeader,
    MatError,
    MatSuffix
  ],
  styleUrls: ['./code-map.component.scss']
})
export class CodeMapComponent implements OnInit, OnDestroy {
  @Input() operation!: IOperationModel;
  @Input() formMode!: FormMode;

  private _viewOnly = false;
  @Input()
  set viewOnly(value: boolean) {
    if (value !== undefined) this._viewOnly = value;
  }
  get viewOnly(): boolean {
    return this._viewOnly;
  }

  @Output() formValueChanged = new EventEmitter<boolean>();
  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  form: FormGroup;

  resourceTypes: string[] = [];

  readonly operationType = OperationType.CodeMap;

  protected readonly FormMode = FormMode;

  destroy$ = new Subject<void>()

  vendors: IVendor[] = [];

  constructor(
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
    private operationService: OperationService
  ) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      facilityId: new FormControl({ value: '', disabled: true }, Validators.required),
      description: new FormControl('', Validators.required),
      name: new FormControl('', Validators.required),
      fhirPath: new FormControl('', Validators.required),
      codeSystemMaps: this.fb.array([], AtLeastOneConditionValidator),
      selectedVendor: new FormControl('')
    }, {validators: facilityOrVendorRequiredValidator});
  }

  ngOnInit(): void {
    this.facilityIdControl.setValue(this.operation.facilityId);
    this.facilityIdControl.updateValueAndValidity();

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

    if (this.formMode === FormMode.Edit) {
      this.patchFormForEdit();
    } else {
      this.addCodeSystemMap(); // Add initial empty for Create mode only
    }

    this.form.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.form.invalid);
    });
  }

  getResourceTypes(): Observable<string[]> {
    return this.operationService.getResourceTypes();
  }


  // Getters for form controls
  get facilityIdControl(): FormControl {
    return this.form.get('facilityId') as FormControl;
  }
  get selectedResourceTypesControl(): FormControl {
    return this.form.get('selectedResourceTypes') as FormControl;
  }
  get descriptionControl(): FormControl {
    return this.form.get('description') as FormControl;
  }
  get nameControl(): FormControl {
    return this.form.get('name') as FormControl;
  }
  get fhirPathControl(): FormControl {
    return this.form.get('fhirPath') as FormControl;
  }
  get codeSystemMaps(): FormArray {
    return this.form.get('codeSystemMaps') as FormArray;
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

  clearFhirPath(): void {
    this.fhirPathControl.setValue('');
    this.fhirPathControl.updateValueAndValidity();
  }

  get isVendorMode(): boolean {
    return !this.operation.facilityId;
  }

  patchFormForEdit(): void {
    const codeMapOp = this.operation.parsedOperationJson as CodeMapOperation;

    this.descriptionControl.setValue(this.operation.description);
    this.nameControl.setValue(codeMapOp?.Name || '');
    this.fhirPathControl.setValue(codeMapOp?.FhirPath || '');
    this.selectedResourceTypesControl.setValue(
      [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
    );

    this.codeSystemMaps.clear();

    if (codeMapOp?.CodeSystemMaps?.length) {
      codeMapOp.CodeSystemMaps.forEach(csMap => {
        const csMapGroup = this.fb.group({
          id: [csMap.id],
          sourceSystem: [csMap.SourceSystem, Validators.required],
          targetSystem: [csMap.TargetSystem, Validators.required],
          codeMaps: this.fb.array([], AtLeastOneConditionValidator),
        });
        // push one code map
        const codeMapsArray = csMapGroup.get('codeMaps') as FormArray;
        Object.entries(csMap.CodeMaps).forEach(([key, value]) => {
          codeMapsArray.push(this.fb.group({
            key: [key, Validators.required],
            value: this.fb.group({
              code: [value.Code, Validators.required],
              display: [value.Display],
            }),
          }));
        });

        this.codeSystemMaps.push(csMapGroup);
      });
    } else {
      // fallback empty map if none present
      this.addCodeSystemMap();
    }
  }

  // CodeSystemMap methods
  addCodeSystemMap(): void {
    const codeSystemMapGroup = this.fb.group({
      sourceSystem: ['', Validators.required],
      targetSystem: ['', Validators.required],
      codeMaps: this.fb.array([
        this.fb.group({
          key: ['', Validators.required],
          value: this.fb.group({
            code: ['', Validators.required],
            display: ['']
          })
        })
      ])
    });

    this.codeSystemMaps.push(codeSystemMapGroup);
  }

  removeCodeSystemMap(index: number): void {
    this.codeSystemMaps.removeAt(index);
  }

  codeMapsAt(index: number): FormArray {
    return this.codeSystemMaps.at(index).get('codeMaps') as FormArray;
  }


  addCodeMap(codeSystemIndex: number): void {
    this.codeMapsAt(codeSystemIndex).push(this.fb.group({
      key: ['', Validators.required],
      value: this.fb.group({
        code: ['', Validators.required],
        display: [''],
      }),
    }));
  }

  removeCodeMap(codeSystemIndex: number, codeMapIndex: number): void {
    this.codeMapsAt(codeSystemIndex).removeAt(codeMapIndex);
  }

  compareResourceTypes(o1: any, o2: any): boolean {
    return o1 === o2;
  }

  private buildCodeSystemMapsPayload(): any[] {
    return this.codeSystemMaps.controls.map((group: AbstractControl) => {
      const fg = group as FormGroup;
      const codeMapsArray = fg.get('codeMaps') as FormArray;

      const codeMaps: Record<string, CodeMap> = {};
      codeMapsArray.controls.forEach(control => {
        const cg = control as FormGroup;
        const key = cg.get('key')?.value;
        const code = cg.get('value.code')?.value;
        const display = cg.get('value.display')?.value;

        if (key && code) {
          codeMaps[key] = { Code: code, Display: display || code };
        }
      });

      return {
        SourceSystem: fg.get('sourceSystem')?.value,
        TargetSystem: fg.get('targetSystem')?.value,
        CodeMaps: codeMaps,
      };
    });
  }

  submitConfiguration(): void {
    this.form.markAllAsTouched();
    this.form.updateValueAndValidity();

    if (!this.form.valid) {
      this.snackBar.open('Invalid form, please check for errors.', '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top',
      });
      return;
    }

    const operationJsonObj = {
      OperationType: this.operationType.toString(),
      Name: this.nameControl.value,
      Description: this.descriptionControl.value,
      FhirPath: this.fhirPathControl.value,
      CodeSystemMaps: this.buildCodeSystemMapsPayload()
    };

    const saveModel: ISaveOperationModel = {
      id: this.operation.id,
      resourceTypes: this.selectedResourceTypesControl.value,
      facilityId: this.operation.facilityId,
      description: this.descriptionControl.value,
      operation: operationJsonObj,
      vendorIds: this.selectedVendorControl?.value ? this.selectedVendorControl?.value : []
    };

    const request$ = this.formMode === FormMode.Create
      ? this.operationService.createOperationConfiguration(saveModel)
      : this.operationService.updateOperationConfiguration(saveModel);

    request$.subscribe({
      next: () => {
        const msg = this.formMode === FormMode.Create ? 'Operation created successfully.' : 'Operation updated successfully.';
        this.submittedConfiguration.emit({ id: this.operation.id || '', message: msg });
      },
      error: () => {
        const errMsg = this.formMode === FormMode.Create ? 'Error creating operation.' : 'Error updating operation.';
        this.submittedConfiguration.emit({ id: '', message: errMsg });
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
