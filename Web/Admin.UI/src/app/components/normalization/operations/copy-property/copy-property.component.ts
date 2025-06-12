import {MatCardContent} from "@angular/material/card";
import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {FormMode} from '../../../../models/FormMode.enum';
import {IEntityCreatedResponse} from '../../../../interfaces/entity-created-response.model';
import {IOperationModel} from '../../../../interfaces/normalization/operation-get-model.interface';
import {MatError, MatFormField, MatInput, MatLabel, MatSuffix} from "@angular/material/input";
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from "@angular/forms";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {ISaveOperationModel, OperationType} from "../../../../interfaces/normalization/operation-save-model.interface";
import {NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {Observable} from "rxjs";
import {MatIconButton} from "@angular/material/button";
import {MatIcon} from "@angular/material/icon";
import {MatCheckbox} from "@angular/material/checkbox";

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
export class CopyPropertyComponent implements OnInit {

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

    // load resource types from api
    this.getResourceTypes().subscribe(resourceTypes => {
      this.resourceTypes = resourceTypes;
    });

    // React to value changes if needed
    this.copyPropertyForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.copyPropertyForm.invalid);
    });

    if (this.operation) {
      let OperationJson: any;
      try {
        OperationJson = JSON.parse(this.operation?.OperationJson || "{}");
      } catch (e) {
        console.error("Invalid JSON in OperationJson", e);
        OperationJson = {};
      }

      this.FacilityIdControl.setValue(this.operation.FacilityId);
      this.FacilityIdControl.updateValueAndValidity();

      this.DescriptionControl.setValue(this.operation.Description);
      this.DescriptionControl.updateValueAndValidity();

      this.IsDisabledControl.setValue(!!this.operation?.IsDisabled);
      this.IsDisabledControl.updateValueAndValidity();

      this.NameControl.setValue(OperationJson.Name);
      this.NameControl.updateValueAndValidity();

      this.SourceFhirPathControl.setValue(OperationJson.SourceFhirPath);
      this.SourceFhirPathControl.updateValueAndValidity();

      this.TargetFhirPathControl.setValue(OperationJson.TargetFhirPath);
      this.TargetFhirPathControl.updateValueAndValidity();

      // get resource types
      this.SelectedReportTypesControl.setValue([...new Set(this.operation?.Resources?.map(r => r.ResourceName) ?? [])]);
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

    if (this.copyPropertyForm.valid) {
      const operationJsonObj = {
        OperationType: OperationType.CopyProperty.toString(),
        Name: this.copyPropertyForm.get('Name')?.value,
        SourceFhirPath: this.copyPropertyForm.get('SourceFhirPath')?.value,
        TargetFhirPath: this.copyPropertyForm.get('TargetFhirPath')?.value
      };

      if (this.formMode == FormMode.Create) {
        this.operationService.createOperationConfiguration({
          ResourceTypes: this.SelectedReportTypesControl.value,
          FacilityId: this.operation.FacilityId,
          Description: this.DescriptionControl.value,
          OperationType: this.operationType,
          Operation: operationJsonObj
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
          Id: this.operation.Id,
          ResourceTypes: this.SelectedReportTypesControl.value,
          FacilityId: this.operation.FacilityId,
          Description: this.DescriptionControl.value,
          IsDisabled: this.IsDisabledControl.value,
          OperationType: this.operationType,
          Operation: operationJsonObj
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

  protected readonly FormMode = FormMode;
}
