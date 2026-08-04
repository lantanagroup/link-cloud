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
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";
import {MatOption, MatSelect} from "@angular/material/select";
import {map, Observable, startWith, Subject, takeUntil} from "rxjs";
import {MatButton, MatIconButton} from "@angular/material/button";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {IVendor} from "../../../../interfaces/tenant/vendor-interface";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";
import {MatAutocomplete, MatAutocompleteTrigger} from "@angular/material/autocomplete";
import {RemoveExtensionsOperation} from "../../../../interfaces/normalization/remove-extensions-operation-interface";
import {MatDialog} from "@angular/material/dialog";
import {
  CsvImportDialogData,
  CsvImportDialogResult,
  RemoveExtensionsCsvImportDialogComponent
} from "./remove-extensions-csv-import-dialog/remove-extensions-csv-import-dialog.component";

function noLeadingTrailingWhitespace(control: AbstractControl): {[key: string]: boolean} | null {
  const v = control.value as string;
  return v && v !== v.trim() ? {whitespace: true} : null;
}

function absoluteUrl(control: AbstractControl): {[key: string]: boolean} | null {
  const v = control.value as string;
  if (!v) return null;
  try {
    new URL(v);
    return null;
  } catch {
    return {absoluteUrl: true};
  }
}

@Component({
  selector: 'app-remove-extensions',
  templateUrl: './remove-extensions.component.html',
  styleUrls: ['./remove-extensions.component.scss'],
  standalone: true,
  imports: [
    MatCardContent,
    MatFormField,
    MatInput,
    MatLabel,
    ReactiveFormsModule,
    MatSelect,
    MatOption,
    MatError,
    MatIcon,
    MatIconButton,
    MatSuffix,
    MatCheckbox,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatButton
  ],
})
export class RemoveExtensionsComponent implements OnInit, OnDestroy, AfterViewInit {

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

  resourceTypes: string[] = [];
  filteredResourceTypes: string[] = [];
  form!: FormGroup;
  vendors: IVendor[] = [];
  errorMessage: string = "";
  urlListTouched: boolean = false;
  userClicked = false;

  protected readonly FormMode = FormMode;
  destroy$ = new Subject<void>();

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService, private dialog: MatDialog) {
    this.form = this.fb.group({
      selectedResourceTypes: new FormControl([], Validators.required),
      resourceType: new FormControl(''),
      facilityId: new FormControl(''),
      name: new FormControl('', Validators.required),
      description: new FormControl(''),
      isEnabled: new FormControl(true),
      selectedVendor: new FormControl([]),
      extensionUrls: this.fb.array([])
    }, {validators: facilityOrVendorRequiredValidator});
  }

  ngOnInit(): void {
    const op = this.operation.parsedOperationJson as RemoveExtensionsOperation;

    this.getResourceTypes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: types => {
          this.resourceTypes = types;
          this.filteredResourceTypes = types;
        },
        error: () =>
          this.snackBar.open('Failed to load resource types', '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          })
      });

    this.resourceTypeControl.valueChanges.pipe(
      startWith(''),
      map(value => this._filter(value || ''))
    ).subscribe(filtered => this.filteredResourceTypes = filtered);

    this.operationService.getVendors().subscribe({
      next: (data) => {
        this.vendors = data;
        if (this.formMode === FormMode.Edit && this.isVendorMode && Array.isArray(this.operation.vendorPresets)) {
          const matchedVendorIds: string[] = [];
          for (const preset of this.operation.vendorPresets) {
            const vendorName = preset.vendorVersion?.vendorName;
            if (vendorName) {
              const match = this.vendors.find(v => v.name === vendorName);
              if (match) matchedVendorIds.push(match.id);
            }
          }
          if (matchedVendorIds.length > 0) {
            this.selectedVendorControl.setValue(matchedVendorIds);
          }
        }
      },
      error: (err) => console.error('Error loading vendors', err)
    });

    this.form.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.form.invalid || this.extensionUrlsArray.length === 0);
    });

    this.facilityIdControl.setValue(this.operation.facilityId);
    this.facilityIdControl.updateValueAndValidity();

    if (!this.isVendorMode) {
      this.facilityIdControl.disable();
    }

    if (this.formMode === FormMode.Edit) {
      this.nameControl.setValue(op?.Name ?? '');
      this.descriptionControl.setValue(this.operation.description ?? '');
      this.isEnabledControl.setValue(!this.operation?.isDisabled);

      this.selectedResourceTypesControl.setValue(
        [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
      );

      (op?.ExtensionUrls ?? []).forEach(url => {
        this.extensionUrlsArray.push(this.fb.control(url, [Validators.required, noLeadingTrailingWhitespace, absoluteUrl]));
      });
    }
  }

  get extensionUrlsArray(): FormArray {
    return this.form.get('extensionUrls') as FormArray;
  }

  addExtensionUrl(): void {
    this.extensionUrlsArray.push(this.fb.control('', [Validators.required, noLeadingTrailingWhitespace, absoluteUrl]));
  }

  removeExtensionUrl(index: number): void {
    this.extensionUrlsArray.removeAt(index);
  }

  get showUrlListError(): boolean {
    return this.urlListTouched && this.extensionUrlsArray.length === 0;
  }

  _filter(value: string): string[] {
    const filterValue = value?.toLowerCase() || '';
    if (!filterValue) return this.resourceTypes.slice();
    return this.resourceTypes.filter(type => type.toLowerCase().startsWith(filterValue));
  }

  openAutocompletePanel(): void {
    if (!this.viewOnly) {
      this.filteredResourceTypes = this.resourceTypes.slice();
      if (this.userClicked) {
        this.trigger.openPanel();
      } else {
        this.trigger.closePanel();
      }
      this.userClicked = false;
    }
  }

  toggleSelection(type: string, selected: boolean): void {
    const current: string[] = this.selectedResourceTypesControl.value || [];
    const updated = selected
      ? [...current, type].filter((v, i, self) => self.indexOf(v) === i)
      : current.filter(t => t !== type);
    this.selectedResourceTypesControl.setValue(updated);
    setTimeout(() => this.trigger.openPanel(), 0);
  }

  ngAfterViewInit(): void {
    this.trigger.panelClosingActions.subscribe((event) => {
      if (!event) this.resourceTypeControl.setValue('');
    });
  }

  onInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && this.trigger?.activeOption) {
      const selectedType = this.trigger.activeOption.value;
      const current: string[] = this.selectedResourceTypesControl.value || [];
      const alreadySelected = current.includes(selectedType);
      this.selectedResourceTypesControl.setValue(
        alreadySelected ? current.filter(t => t !== selectedType) : [...current, selectedType]
      );
      setTimeout(() => this.trigger.openPanel(), 0);
      event.preventDefault();
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

  clearName(): void {
    this.nameControl.setValue('');
  }

  clearDescription(): void {
    this.descriptionControl.setValue('');
  }

  submitConfiguration(): void {
    this.urlListTouched = true;
    this.form.markAllAsTouched();

    if (this.form.invalid || this.extensionUrlsArray.length === 0) {
      return;
    }

    const operationJsonObj: RemoveExtensionsOperation = {
      OperationType: OperationType.RemoveExtensions.toString(),
      Name: this.nameControl.value,
      Description: this.descriptionControl.value,
      ExtensionUrls: this.extensionUrlsArray.controls.map((c: AbstractControl) => c.value)
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

  openCsvImportDialog(): void {
    const dialogData: CsvImportDialogData = {
      facilityId: this.operation.facilityId,
      isVendorMode: this.isVendorMode,
      vendorIds: this.selectedVendorControl.value ?? [],
      validResourceTypes: this.resourceTypes
    };

    this.dialog.open(RemoveExtensionsCsvImportDialogComponent, {
      data: dialogData,
      width: '700px',
      disableClose: false
    }).afterClosed().subscribe((result: CsvImportDialogResult | null) => {
      if (result && result.created > 0) {
        const msg = result.failed > 0
          ? `${result.created} operation(s) created. ${result.failed} failed.`
          : `${result.created} operation(s) created successfully.`;
        this.submittedConfiguration.emit({id: '', message: msg});
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
