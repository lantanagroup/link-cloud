import {MatCardContent} from "@angular/material/card";
import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  OnInit,
  Output,
  ViewChild
} from '@angular/core';
import {FormMode} from '../../../../models/FormMode.enum';
import {IEntityCreatedResponse} from '../../../../interfaces/entity-created-response.model';

import {MatError, MatFormField, MatInput, MatLabel, MatSuffix} from "@angular/material/input";
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";
import {NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {map, Observable, of, startWith, Subject, takeUntil} from "rxjs";
import {MatIconButton} from "@angular/material/button";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";
import {CopyPropertyOperation} from "../../../../interfaces/normalization/copy-property-interface";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";
import {MatAutocomplete, MatAutocompleteTrigger} from "@angular/material/autocomplete";

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
    MatCheckbox,
    MatAutocomplete,
    MatAutocompleteTrigger
  ],
})
export class CopyPropertyComponent implements OnInit, OnDestroy, AfterViewInit  {

  @ViewChild('errorDiv') errorDiv!: ElementRef;
  @ViewChild(MatAutocompleteTrigger) trigger!: MatAutocompleteTrigger;

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

  vendors: IVendor[] = [];

  errorMessage: string = "";

  filteredResourceTypes: string[] = [];

  userClicked = false;

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      resourceType: new FormControl(''),
      facilityId: new FormControl(''),
      description: new FormControl(''),
      name: new FormControl('', Validators.required),
      isEnabled: new FormControl(true),
      sourceFhirPath: new FormControl('', Validators.required),
      targetFhirPath: new FormControl('', Validators.required),
      selectedVendor: new FormControl([])
    }, {validators: facilityOrVendorRequiredValidator});
  }

  ngOnInit(): void {

    const copyPropertyOperation = this.operation.parsedOperationJson as CopyPropertyOperation;

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

    this.filteredResourceTypes = this.resourceTypes;

    this.resourceTypeControl.valueChanges.pipe(
      startWith(''),
      map(value => this._filter(value || ''))
    ).subscribe(filtered => this.filteredResourceTypes = filtered);

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

    // React to value changes if needed
    this.form.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.form.invalid);
    });

    // set the facilityId regardless of Edit/Add
    this.facilityIdControl.setValue(this.operation.facilityId);
    this.facilityIdControl.updateValueAndValidity();

    if (!this.isVendorMode) {
      this.facilityIdControl.disable(); // only disables input, not removes from validator
    }

    if (this.formMode === FormMode.Edit) {

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
      this.selectedResourceTypesControl.setValue(
        [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
      );
      this.selectedResourceTypesControl.updateValueAndValidity();

    }
  }
  _filter(value: string): string[] {
    const filterValue = value?.toLowerCase() || '';
    if (!filterValue) {
      return this.resourceTypes.slice(); // all resources when empty input
    }
    return this.resourceTypes.filter(type => type.toLowerCase().startsWith(filterValue));
  }

  openAutocompletePanel() {
    if (!this.viewOnly) {
      // Reset the filter to show all
      this.filteredResourceTypes = this.resourceTypes.slice();
      if (this.userClicked) {
        this.trigger.openPanel();
      } else {
        this.trigger.closePanel(); // Prevent accidental opening
      }
      this.userClicked = false;
    }
  }

  toggleSelection(type: string, selected: boolean): void {
    const currentSelection: string[] = this.selectedResourceTypesControl.value || [];

    const updatedSelection = selected
      ? [...currentSelection, type].filter((v, i, self) => self.indexOf(v) === i)
      : currentSelection.filter(t => t !== type);

    this.selectedResourceTypesControl.setValue(updatedSelection);

    setTimeout(() => this.trigger.openPanel(), 0);
  }

  ngAfterViewInit(): void {
    this.trigger.panelClosingActions.subscribe((event) => {
      // Only clear input if no option was selected (i.e., click outside or ESC)
      if (!event) {
        this.resourceTypeControl.setValue('');
      }
    });
  }

  onInputKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && this.trigger?.activeOption) {
      const selectedType = this.trigger.activeOption.value;
      const currentValues: string[] = this.selectedResourceTypesControl.value || [];

      const alreadySelected = currentValues.includes(selectedType);
      const updatedValues = alreadySelected
        ? currentValues.filter(t => t !== selectedType)
        : [...currentValues, selectedType];

      this.selectedResourceTypesControl.setValue(updatedValues);

      setTimeout(() => this.trigger.openPanel(), 0);
      event.preventDefault(); // prevent closing
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

  get isEnabledControl(): FormControl {
    return this.form.get('isEnabled') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.form.get('facilityId') as FormControl;
  }

  get resourceTypeControl(): FormControl {
    return this.form.get('resourceType') as FormControl;
  }

  get sourceFhirPathControl(): FormControl {
    return this.form.get('sourceFhirPath') as FormControl;
  }

  get targetFhirPathControl(): FormControl {
    return this.form.get('targetFhirPath') as FormControl;
  }

  get selectedVendorControl(): FormControl {
    return this.form.get('selectedVendor') as FormControl;
  }

  get isVendorMode(): boolean {
    return !this.operation.facilityId;
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

    const operationJsonObj: CopyPropertyOperation = {
      OperationType: OperationType.CopyProperty.toString(),
      Name: this.form.get('name')?.value,
      Description: this.form.get('description')?.value,
      SourceFhirPath: this.form.get('sourceFhirPath')?.value,
      TargetFhirPath: this.form.get('targetFhirPath')?.value
    };

    const saveModel: ISaveOperationModel = {
      id: this.formMode === FormMode.Edit ? this.operation?.id : undefined,
      facilityId: this.operation?.facilityId,
      description: this.descriptionControl.value,
      resourceTypes: this.selectedResourceTypesControl.value,
      operation: operationJsonObj,
      isDisabled: !this.isEnabledControl?.value,
      vendorIds: this.selectedVendorControl?.value ? this.selectedVendorControl?.value : []
    };

    const request$ = this.formMode === FormMode.Create ? this.operationService.createOperationConfiguration(saveModel) : this.operationService.updateOperationConfiguration(saveModel);

    request$.subscribe({
      next: () => {
        const msg = this.formMode === FormMode.Create ? 'Operation created successfully.' : 'Operation updated successfully.';
        this.submittedConfiguration.emit({id: '', message: msg});
      },
      error: (err) => {
        const errorMessage = this.formMode === FormMode.Create ? 'Error creating operation.' + (err?.message || 'Unknown error') : 'Error updating operation.' + (err?.message || 'Unknown error');
        this.showError(errorMessage);
      }
    });
  }

  showError(message: string) {
    this.errorMessage = message;

    // Give Angular time to render the div
    setTimeout(() => {
      this.errorDiv?.nativeElement.scrollIntoView({behavior: 'smooth', block: 'center'});
      this.errorDiv?.nativeElement.focus?.(); // Optional for accessibility
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
