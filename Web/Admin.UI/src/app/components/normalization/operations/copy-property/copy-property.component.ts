import {MatCardContent} from "@angular/material/card";
import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {FormMode} from '../../../../models/FormMode.enum';
import {IEntityCreatedResponse} from '../../../../interfaces/entity-created-response.model';
import {IOperationModel} from '../../../../interfaces/normalization/operation-get-model.interface';
import {MatError, MatFormField, MatInput, MatLabel, MatSuffix} from "@angular/material/input";
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";
import {NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {Observable, of, Subject, takeUntil} from "rxjs";
import {MatIconButton} from "@angular/material/button";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";
import {CopyPropertyOperation} from "../../../../interfaces/normalization/copy-property-interface";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";

@Component({
  selector: 'app-copy-property',
  templateUrl: './copy-property.component.html',
  styleUrls: ['./copy-property.component.scss'],
  standalone: true,
  imports: [
    MatCardContent,
    MatFormField,
    MatInput,
    MatLabel,
    ReactiveFormsModule,
    NgForOf,
    MatSelect,
    MatOption,
    MatError,
    MatIcon,
    MatIconButton,
    MatSuffix,
    NgIf,
    MatCheckbox
  ],
})
export class CopyPropertyComponent implements OnInit, OnDestroy  {

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

  operationType: OperationType = OperationType.CopyProperty;

  resourceTypes: string[] = [];

  copyPropertyForm!: FormGroup;

  protected readonly FormMode = FormMode;

  destroy$ = new Subject<void>()

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
    this.copyPropertyForm = this.fb.group({
      SelectedResourceTypes: new FormControl([], Validators.required),
      FacilityId: new FormControl({value: '', disabled: true}, Validators.required),
      Description: new FormControl('', Validators.required),
      Name: new FormControl('', Validators.required),
      IsDisabled: new FormControl(false),
      SourceFhirPath: new FormControl('', Validators.required),
      TargetFhirPath: new FormControl('', Validators.required)
    });
  }

  ngOnInit(): void {

    const copyPropertyOperation = this.operation.operationJson as CopyPropertyOperation;

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

    // React to value changes if needed
    this.copyPropertyForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.copyPropertyForm.invalid);
    });

    if (this.formMode === FormMode.Edit) {
      this.FacilityIdControl.setValue(this.operation.facilityId);
      this.FacilityIdControl.updateValueAndValidity();

      this.DescriptionControl.setValue(this.operation.description);
      this.DescriptionControl.updateValueAndValidity();

      this.IsDisabledControl.setValue(!!this.operation?.isDisabled);
      this.IsDisabledControl.updateValueAndValidity();

      this.NameControl.setValue(copyPropertyOperation.Name);
      this.NameControl.updateValueAndValidity();

      this.SourceFhirPathControl.setValue(copyPropertyOperation.SourceFhirPath);
      this.SourceFhirPathControl.updateValueAndValidity();

      this.TargetFhirPathControl.setValue(copyPropertyOperation.TargetFhirPath);
      this.TargetFhirPathControl.updateValueAndValidity();

      // get resource types
      this.SelectedReportTypesControl.setValue([...new Set(this.operation?.resources?.map(r => r.resourceName) ?? [])]);
      this.SelectedReportTypesControl.updateValueAndValidity();
    }
  }

  getResourceTypes(): Observable<string[]> {
    return this.operationService.getResourceTypes();
  }

  get SelectedReportTypesControl(): FormControl {
    return this.copyPropertyForm.get('SelectedResourceTypes') as FormControl;
  }

  get NameControl(): FormControl {
    return this.copyPropertyForm.get('Name') as FormControl;
  }

  get DescriptionControl(): FormControl {
    return this.copyPropertyForm.get('Description') as FormControl;
  }

  get IsDisabledControl(): FormControl {
    return this.copyPropertyForm.get('IsDisabled') as FormControl;
  }

  get FacilityIdControl(): FormControl {
    return this.copyPropertyForm.get('FacilityId') as FormControl;
  }

  get SourceFhirPathControl(): FormControl {
    return this.copyPropertyForm.get('SourceFhirPath') as FormControl;
  }

  get TargetFhirPathControl(): FormControl {
    return this.copyPropertyForm.get('TargetFhirPath') as FormControl;
  }

  clearName(): void {
    this.NameControl.setValue('');
    this.NameControl.updateValueAndValidity();
  }

  clearSourcePath(): void {
    this.SourceFhirPathControl.setValue('');
    this.SourceFhirPathControl.updateValueAndValidity();
  }

  clearTargetPath(): void {
    this.TargetFhirPathControl.setValue('');
    this.TargetFhirPathControl.updateValueAndValidity();
  }

  clearDescription(): void {
    this.DescriptionControl.setValue('');
    this.DescriptionControl.updateValueAndValidity();
  }

  compareResourceTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  submitConfiguration(): void {
    if (!this.copyPropertyForm.valid) {
      this.snackBar.open('Invalid form, please check for errors.', '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
      return;
    }

    const operationJsonObj: CopyPropertyOperation = {
      OperationType: OperationType.CopyProperty.toString(),
      Name: this.copyPropertyForm.get('Name')?.value,
      Description: this.copyPropertyForm.get('Description')?.value,
      SourceFhirPath: this.copyPropertyForm.get('SourceFhirPath')?.value,
      TargetFhirPath: this.copyPropertyForm.get('TargetFhirPath')?.value
    };

    const model: ISaveOperationModel = {
      id: this.formMode === FormMode.Edit ? this.operation?.id : undefined,
      facilityId: this.operation?.facilityId,
      description: this.DescriptionControl.value,
      resourceTypes: this.SelectedReportTypesControl.value,
      operation: operationJsonObj,
      isDisabled: this.IsDisabledControl?.value
    };

    if (this.formMode === FormMode.Create) {
      this.operationService.createOperationConfiguration(model).subscribe({
        next: () => {
          this.submittedConfiguration.emit({ id: '', message: 'Operation created successfully.' });
        },
        error: () => {
          this.submittedConfiguration.emit({ id: '', message: 'Error creating operation.' });
        }
      });
    } else {
      this.operationService.updateOperationConfiguration(model).subscribe({
        next: () => {
          this.submittedConfiguration.emit({ id: '', message: 'Operation updated successfully.' });
        },
        error: () => {
          this.submittedConfiguration.emit({ id: '', message: 'Error updating operation.' });
        }
      });
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
