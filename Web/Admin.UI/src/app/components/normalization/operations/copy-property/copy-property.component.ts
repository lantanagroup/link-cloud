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

  form!: FormGroup;

  protected readonly FormMode = FormMode;

  destroy$ = new Subject<void>()

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      facilityId: new FormControl({value: '', disabled: true}, Validators.required),
      description: new FormControl('', Validators.required),
      name: new FormControl('', Validators.required),
      isEnabled: new FormControl(true),
      sourceFhirPath: new FormControl('', Validators.required),
      targetFhirPath: new FormControl('', Validators.required)
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
    this.form.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.form.invalid);
    });
    

    if (this.formMode === FormMode.Edit) {
      this.facilityIdControl.setValue(this.operation.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.descriptionControl.setValue(this.operation.description);
      this.descriptionControl.updateValueAndValidity();

      this.isEnabledControl.setValue(!this.operation?.isDisabled);
      this.isEnabledControl.updateValueAndValidity();

      this.nameControl.setValue(copyPropertyOperation.Name);
      this.nameControl.updateValueAndValidity();

      this.sourceFhirPathControl.setValue(copyPropertyOperation.SourceFhirPath);
      this.sourceFhirPathControl.updateValueAndValidity();

      this.targetFhirPathControl.setValue(copyPropertyOperation.TargetFhirPath);
      this.targetFhirPathControl.updateValueAndValidity();

      // get resource types
      this.selectedReportTypesControl.setValue([...new Set(this.operation?.resources?.map(r => r.resourceName) ?? [])]);
      this.selectedReportTypesControl.updateValueAndValidity();
    }
  }

  getResourceTypes(): Observable<string[]> {
    return this.operationService.getResourceTypes();
  }

  get selectedReportTypesControl(): FormControl {
    return this.form.get('selectedResourceTypes') as FormControl;
  }

  get nameControl(): FormControl {
    return this.form.get('name') as FormControl;
  }

  get descriptionControl(): FormControl {
    return this.form.get('description') as FormControl;
  }

  get isEnabledControl(): FormControl {
    return this.form.get('isEnabled') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.form.get('facilityId') as FormControl;
  }

  get sourceFhirPathControl(): FormControl {
    return this.form.get('sourceFhirPath') as FormControl;
  }

  get targetFhirPathControl(): FormControl {
    return this.form.get('targetFhirPath') as FormControl;
  }

  clearName(): void {
    this.nameControl.setValue('');
    this.nameControl.updateValueAndValidity();
  }

  clearSourcePath(): void {
    this.sourceFhirPathControl.setValue('');
    this.sourceFhirPathControl.updateValueAndValidity();
  }

  clearTargetPath(): void {
    this.targetFhirPathControl.setValue('');
    this.targetFhirPathControl.updateValueAndValidity();
  }

  clearDescription(): void {
    this.descriptionControl.setValue('');
    this.descriptionControl.updateValueAndValidity();
  }

  compareResourceTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  isEnabled(op: any): string {
    return !op.isDisabled ? 'Yes' : 'No';
  }

  submitConfiguration(): void {
    if (!this.form.valid) {
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
      Name: this.form.get('name')?.value,
      Description: this.form.get('description')?.value,
      SourceFhirPath: this.form.get('sourceFhirPath')?.value,
      TargetFhirPath: this.form.get('targetFhirPath')?.value
    };

    const model: ISaveOperationModel = {
      id: this.formMode === FormMode.Edit ? this.operation?.id : undefined,
      facilityId: this.operation?.facilityId,
      description: this.descriptionControl.value,
      resourceTypes: this.selectedReportTypesControl.value,
      operation: operationJsonObj,
      isDisabled: !this.isEnabledControl?.value
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
