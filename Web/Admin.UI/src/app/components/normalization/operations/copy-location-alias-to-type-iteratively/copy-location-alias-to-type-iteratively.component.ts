import {MatCardContent} from "@angular/material/card";
import {Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {Observable, Subject, takeUntil} from "rxjs";
import {MatFormField, MatInput, MatLabel} from "@angular/material/input";
import {MatOption, MatSelect} from "@angular/material/select";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";

import {IEntityCreatedResponse} from '../../../../interfaces/entity-created-response.model';
import {CopyLocationAliasToTypeIterativelyOperation} from "../../../../interfaces/normalization/copy-location-alias-to-type-iteratively-operation-interface";
import {FormMode} from '../../../../models/FormMode.enum';
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";

@Component({
  selector: 'app-copy-location-alias-to-type-iteratively',
  templateUrl: './copy-location-alias-to-type-iteratively.component.html',
  styleUrls: ['./copy-location-alias-to-type-iteratively.component.scss'],
  standalone: true,
  imports: [
    MatCardContent,
    MatFormField,
    MatInput,
    MatLabel,
    ReactiveFormsModule,
    MatSelect,
    MatOption,
    MatIcon,
    MatCheckbox
  ],
})
export class CopyLocationAliasToTypeIterativelyComponent implements OnInit, OnDestroy {

  @ViewChild('errorDiv') errorDiv!: ElementRef;

  @Input() operation!: IOperationModel;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;

  @Input()
  set viewOnly(v: boolean) {
    this._viewOnly = v ?? false;
  }

  get viewOnly(): boolean {
    return this._viewOnly;
  }

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();
  @Output() formValueChanged = new EventEmitter<boolean>();

  resourceTypes: string[] = [];
  form: FormGroup;
  vendors: IVendor[] = [];
  errorMessage: string = "";
  destroy$ = new Subject<void>();

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      facilityId: new FormControl(''),
      name: new FormControl("Copy Location Alias to Type Iteratively Operation", Validators.required),
      description: new FormControl("Copies Location Alias fields into Location.Type as a CodeableConcept. This also copies all parent Locations' aliases in the partOf hierarchy."),
      maxIterations: new FormControl(15, [Validators.required, Validators.min(1)]),
      splitOnComma: new FormControl(false),
      isEnabled: new FormControl(true),
      selectedVendor: new FormControl([])
    }, {validators: facilityOrVendorRequiredValidator});
  }

  ngOnInit(): void {
    const op = this.operation.parsedOperationJson as CopyLocationAliasToTypeIterativelyOperation;

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

    this.operationService.getVendors()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
      next: (data) => {
        this.vendors = data;
        if (this.formMode === FormMode.Edit && this.isVendorMode && Array.isArray(this.operation.vendorPresets)) {
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
      },
      error: (err) => {
        console.error('Error loading vendors', err);
        this.snackBar.open('Failed to load vendors', '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });

    this.form.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.formValueChanged.emit(this.form.invalid);
    });

    this.facilityIdControl.setValue(this.operation.facilityId);
    this.facilityIdControl.updateValueAndValidity();

    if (!this.isVendorMode) {
      this.facilityIdControl.disable();
    }

    if (this.formMode === FormMode.Edit) {
      this.nameControl.setValue(op?.Name ?? '');
      this.descriptionControl.setValue(this.operation.description ?? op?.Description ?? '');
      this.maxIterationsControl.setValue(op?.MaxIterations ?? 15);
      this.splitOnCommaControl.setValue(op?.SplitOnComma ?? false);
      this.isEnabledControl.setValue(!this.operation?.isDisabled);
      this.selectedResourceTypesControl.setValue(
        [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
      );
    }
  }

  getResourceTypes(): Observable<string[]> {
    return this.operationService.getResourceTypes();
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

  get maxIterationsControl(): FormControl {
    return this.form.get('maxIterations') as FormControl;
  }

  get splitOnCommaControl(): FormControl {
    return this.form.get('splitOnComma') as FormControl;
  }

  get isEnabledControl(): FormControl {
    return this.form.get('isEnabled') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.form.get('facilityId') as FormControl;
  }

  get selectedVendorControl(): FormControl {
    return this.form.get('selectedVendor') as FormControl;
  }

  get isVendorMode(): boolean {
    return !this.operation.facilityId;
  }

  get showFacilityOrVendorError(): boolean {
    const facilityCtrl = this.form.get('facilityId');
    const vendorCtrl = this.form.get('selectedVendor');
    const hasError = this.form.hasError('facilityOrVendorRequired');
    const interacted = !!facilityCtrl?.touched || !!vendorCtrl?.touched || !!facilityCtrl?.dirty || !!vendorCtrl?.dirty;
    return hasError && interacted;
  }

  submitConfiguration(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const operationJsonObj: CopyLocationAliasToTypeIterativelyOperation = {
      OperationType: OperationType.CopyLocationAliasToTypeIteratively.toString(),
      Name: this.nameControl.value,
      Description: this.descriptionControl.value,
      MaxIterations: this.maxIterationsControl.value,
      SplitOnComma: this.splitOnCommaControl.value
    };

    const saveModel: ISaveOperationModel = {
      id: this.formMode === FormMode.Edit ? this.operation?.id : undefined,
      facilityId: this.operation?.facilityId,
      description: this.descriptionControl.value,
      resourceTypes: this.selectedResourceTypesControl.value,
      operation: operationJsonObj,
      isDisabled: !this.isEnabledControl?.value,
      vendorIds: this.selectedVendorControl?.value ?? []
    };

    const request$ = this.formMode === FormMode.Create
      ? this.operationService.createOperationConfiguration(saveModel)
      : this.operationService.updateOperationConfiguration(saveModel);

    request$.subscribe({
      next: () => {
        const msg = this.formMode === FormMode.Create ? 'Operation created successfully.' : 'Operation updated successfully.';
        this.submittedConfiguration.emit({id: '', message: msg});
      },
      error: (err) => {
        const action = this.formMode === FormMode.Create ? 'creating' : 'updating';
        this.showError(`Error ${action} operation: ${err?.message ?? 'Unknown error'}`);
      }
    });
  }

  showError(message: string): void {
    this.errorMessage = message;
    setTimeout(() => {
      this.errorDiv?.nativeElement.scrollIntoView({behavior: 'smooth', block: 'center'});
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
