import {Component, EventEmitter, Input, Output, SimpleChanges} from '@angular/core';
import {FormMode} from "../../../models/FormMode.enum";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";
import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from "@angular/forms";
import {MatSnackBar, MatSnackBarModule} from "@angular/material/snack-bar";
import {INormalizationModel} from "../../../interfaces/normalization/normalization-model.interface";
import {NormalizationService} from "../../../services/gateway/normalization/normalization.service";
import {CommonModule} from "@angular/common";
import {MatButtonModule} from "@angular/material/button";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatInputModule} from "@angular/material/input";
import {MatIconModule} from "@angular/material/icon";
import {MatChipsModule} from "@angular/material/chips";
import {MatSelectModule} from "@angular/material/select";
import {MatSlideToggleModule} from "@angular/material/slide-toggle";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatCardModule} from "@angular/material/card";
import {MatTabsModule} from "@angular/material/tabs";
import {MatExpansionModule} from "@angular/material/expansion";
import {MatProgressSpinnerModule} from "@angular/material/progress-spinner";
import {IDataAcquisitionQueryConfigModel} from "../../../interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface";

@Component({
  selector: 'app-normalization-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    CommonModule,
    MatSnackBarModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatToolbarModule,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './normalization.component.html',
  styleUrl: './normalization.component.scss'
})
export class NormalizationFormComponent {

  @Input() item!: INormalizationModel;

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

  normalizationForm: FormGroup;

  isInvalidJson = false;

  constructor(private snackBar: MatSnackBar, private normalizationService: NormalizationService, private fb: FormBuilder) {

    this.normalizationForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      operationSequence: new FormControl('', Validators.required)
    });
  }

  ngOnInit(): void {
    this.normalizationForm.reset();

    if (this.item) {
      //set form values
      this.facilityIdControl.setValue(this.item.FacilityId);
      this.facilityIdControl.updateValueAndValidity();
      let operation = JSON.stringify(this.item.OperationSequence);
      // replace any occurance of $type with short name
      this.operationSequenceControl.setValue(this.item?.OperationSequence ? JSON.stringify(this.item.OperationSequence, null, 2) : '');
      this.operationSequenceControl.updateValueAndValidity();

    } else {
      this.formMode = FormMode.Create;
    }

    this.normalizationForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.normalizationForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {

      this.facilityIdControl.setValue(this.item.FacilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.operationSequenceControl.setValue(this.item?.OperationSequence ? JSON.stringify(this.item.OperationSequence, null, 2) : '')
      this.operationSequenceControl.updateValueAndValidity();
    }
    // toggle view
    this.toggleViewOnly(this.viewOnly);
  }


  get operationSequenceControl(): FormControl {
    return this.normalizationForm.get('operationSequence') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.normalizationForm.get('facilityId') as FormControl;
  }

  clearOperationSequenceControl(): void {
    this.operationSequenceControl.setValue('');
    this.operationSequenceControl.updateValueAndValidity();
  }

  jsonValidator(control: AbstractControl) {
    if (!control.value) {
      return null; // Don't validate empty input (handled by 'required')
    }

    try {
      JSON.parse(control.value);
      return null; // Valid JSON
    } catch {
      return {invalidJson: true}; // ❌ Invalid JSON
    }
  }


  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.operationSequenceControl.disable();
    } else {
      this.operationSequenceControl.enable();
    }
  }


  submitConfiguration(): void {

    if (this.normalizationForm.valid) {
      try {
        const parsedOperation = JSON.parse(this.operationSequenceControl.value);
        if (this.formMode == FormMode.Create) {
          this.normalizationService.createNormalizationConfiguration(this.facilityIdControl.value, {
            FacilityId: this.facilityIdControl.value,
            OperationSequence: parsedOperation
          } as INormalizationModel).subscribe({
            next: (response: IEntityCreatedResponse) => {
              this.submittedConfiguration.emit({id: '', message: `Normalization for ${this.facilityIdControl.value} created successfully`});
            },
            error: (error) => {
              this.snackBar.open(`Failed to create normalization: ${error.message}`, '', {
                duration: 3500,
                panelClass: 'error-snackbar',
                horizontalPosition: 'end',
                verticalPosition: 'top'
              });
            }
          });
        } else if (this.formMode == FormMode.Edit) {
          this.normalizationService.updateNormalizationConfiguration(
            this.facilityIdControl.value,
            {
              FacilityId: this.facilityIdControl.value,
              OperationSequence: parsedOperation
            } as INormalizationModel).subscribe({
              next: (response: IEntityCreatedResponse) => {
                this.submittedConfiguration.emit({id:  '', message: `Normalization for ${this.facilityIdControl.value} updated successfully`});
              },
              error: (error) => {
                this.snackBar.open(`Failed to update normalization: ${error.message}`, '', {
                  duration: 3500,
                  panelClass: 'error-snackbar',
                  horizontalPosition: 'end',
                  verticalPosition: 'top'
                });
              }
            }
          );
        }
      } catch (error) {
        this.snackBar.open(`Invalid JSON in operation sequence: ${(error as Error).message}`, '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
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
}
