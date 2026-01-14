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

import {MatFormField, MatInput, MatLabel} from "@angular/material/input";
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {ISaveOperationModel} from "../../../../interfaces/normalization/operation-save-model.interface";

import {MatOption, MatSelect} from "@angular/material/select";
import {Observable, Subject, takeUntil} from "rxjs";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {facilityOrVendorRequiredValidator} from "../validators/facilityOrVendorRequiredValidator";
import {MatAutocompleteTrigger} from "@angular/material/autocomplete";
import {IOperation} from "../../../../interfaces/normalization/operation.interface";

@Component({
    selector: 'app-copy-location',
    templateUrl: './copy-location.component.html',
    styleUrls: ['./copy-location.component.scss'],
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
export class CopyLocationComponent implements OnInit, OnDestroy {

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

    constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private operationService: OperationService) {
        this.form = this.fb.group({
            selectedResourceTypes: new FormControl([]),
            facilityId: new FormControl(''),
            name: new FormControl("Copy Location"),
            description: new FormControl('Copies each Location Identifier System and Value fields into Location.Type as a Codeable Concept'),
            isEnabled: new FormControl(true),
            selectedVendor: new FormControl([])
        }, {validators: facilityOrVendorRequiredValidator});
    }

    ngOnInit(): void {

        const copyLocationOperation = this.operation.parsedOperationJson as IOperation;

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

        // React to value changes if needed
        this.form.valueChanges.subscribe(() => {
            this.formValueChanged.emit(this.form.invalid);
        });

        // set the facilityId regardless of Edit/Add
        this.facilityIdControl.setValue(this.operation.facilityId);
        this.facilityIdControl.updateValueAndValidity();

        if (!this.isVendorMode) {
            this.facilityIdControl.disable();
        }

        this.nameControl.disable();
        this.descriptionControl.disable();

        if (this.formMode === FormMode.Edit) {

            this.descriptionControl.setValue(this.operation.description);
            this.descriptionControl.updateValueAndValidity();

            this.isEnabledControl.setValue(!this.operation?.isDisabled);
            this.isEnabledControl.updateValueAndValidity();

            this.nameControl.setValue(copyLocationOperation.Name);
            this.nameControl.updateValueAndValidity();

            // get resource types
            this.selectedResourceTypesControl.setValue(
                [...new Set(this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName) ?? [])]
            );
            this.selectedResourceTypesControl.updateValueAndValidity();

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

        const interacted =
            !!facilityCtrl?.touched ||
            !!vendorCtrl?.touched ||
            !!facilityCtrl?.dirty ||
            !!vendorCtrl?.dirty;

        return hasError && interacted;
    }

    isEnabled(op: any): string {
        return !op.isDisabled ? 'Yes' : 'No';
    }

    submitConfiguration(): void {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const operationJsonObj = {
            OperationType: OperationType.CopyLocation.toString(),
            Name:  this.form.get('name')?.value,
            Description:  this.form.get('description')?.value,
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
