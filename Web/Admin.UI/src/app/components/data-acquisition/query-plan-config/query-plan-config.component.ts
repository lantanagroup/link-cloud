import {Component, EventEmitter, Input, Output, SimpleChanges} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from "@angular/forms";
import {IQueryPlanModel} from "../../../interfaces/data-acquisition/query-plan-model.interface";
import {FormMode} from "../../../models/FormMode.enum";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";
import {CommonModule} from "@angular/common";
import {MatButtonModule} from "@angular/material/button";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatInputModule} from "@angular/material/input";
import {MatIconModule} from "@angular/material/icon";
import {MatChipsModule} from "@angular/material/chips";
import {MatSelectModule} from "@angular/material/select";
import {MatSlideToggleModule} from "@angular/material/slide-toggle";
import {MatSnackBar, MatSnackBarModule} from "@angular/material/snack-bar";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatCardModule} from "@angular/material/card";
import {MatTabsModule} from "@angular/material/tabs";
import {MatExpansionModule} from "@angular/material/expansion";
import {MatProgressSpinnerModule} from "@angular/material/progress-spinner";
import {DataAcquisitionService} from "../../../services/gateway/data-acquisition/data-acquisition.service";
import {IDataAcquisitionQueryConfigModel} from "../../../interfaces/data-acquisition/data-acquisition-fhir-query-config-model.interface";
import {IDataAcquisitionFhirListConfigModel} from "../../../interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface";

@Component({
  selector: 'app-query-plan-config-form',
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
  templateUrl: './query-plan-config.component.html',
  styleUrl: './query-plan-config.component.scss'
})
export class QueryPlanConfigFormComponent {

  @Input() item!: IQueryPlanModel;

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

  planForm: FormGroup;

  isInvalidJson = false;

  types: string[] = ["0", "1", "2", "3"];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService, private fb: FormBuilder) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.planForm = new FormGroup({
      planName: new FormControl('', Validators.required),
      facilityId: new FormControl('', Validators.required),
      ehrDescription: new FormControl('', Validators.required),
      lookBack: new FormControl('', Validators.required),
      initialQueries: new FormControl('', [Validators.required, this.jsonValidator]),
      supplementalQueries: new FormControl('', [Validators.required, this.jsonValidator])
    });
  }

  ngOnInit(): void {
    this.planForm.reset();

    if (this.item) {
      //set form values
      this.planNameControl.setValue(this.item.PlanName);
      this.planNameControl.updateValueAndValidity();

      this.facilityIdControl.setValue(this.item.FacilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.ehrDescriptionControl.setValue(this.item.EHRDescription);
      this.ehrDescriptionControl.updateValueAndValidity();

      this.lookBackControl.setValue(this.item.LookBack);
      this.lookBackControl.updateValueAndValidity();


      this.initialQueriesControl.setValue(this.item?.InitialQueries ? JSON.stringify(this.item.InitialQueries, null, 2) : '');
      this.initialQueriesControl.updateValueAndValidity();

      this.supplementalQueriesControl.setValue(this.item?.SupplementalQueries ? JSON.stringify(this.item.SupplementalQueries, null, 2) : '')
      this.supplementalQueriesControl.updateValueAndValidity();

    } else {
      this.formMode = FormMode.Create;
    }

    this.planForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.planForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {

      this.planNameControl.setValue(this.item.PlanName);
      this.planNameControl.updateValueAndValidity();

      this.facilityIdControl.setValue(this.item.FacilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.ehrDescriptionControl.setValue(this.item.EHRDescription);
      this.ehrDescriptionControl.updateValueAndValidity();

      this.lookBackControl.setValue(this.item.LookBack);
      this.lookBackControl.updateValueAndValidity();


      this.initialQueriesControl.setValue(this.item?.InitialQueries ? JSON.stringify(this.item.InitialQueries, null, 2) : '');
      this.initialQueriesControl.updateValueAndValidity();

      this.supplementalQueriesControl.setValue(this.item?.SupplementalQueries ? JSON.stringify(this.item.SupplementalQueries, null, 2) : '')
      this.supplementalQueriesControl.updateValueAndValidity();
    }
    // toggle view
    this.toggleViewOnly(this.viewOnly);
  }

  // Getter methods for form controls
  get planNameControl(): FormControl {
    return this.planForm.get('planName') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.planForm.get('facilityId') as FormControl;
  }

  get ehrDescriptionControl(): FormControl {
    return this.planForm.get('ehrDescription') as FormControl;
  }

  get lookBackControl(): FormControl {
    return this.planForm.get('lookBack') as FormControl;
  }

  get initialQueriesControl(): FormControl {
    return this.planForm.get('initialQueries') as FormControl;
  }

  get supplementalQueriesControl(): FormControl {
    return this.planForm.get('supplementalQueries') as FormControl;
  }


  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  clearPlanName(): void {
    this.planNameControl.setValue('');
    this.planNameControl.updateValueAndValidity();
  }

  clearEhrDescription(): void {
    this.ehrDescriptionControl.setValue('');
    this.ehrDescriptionControl.updateValueAndValidity();
  }

  clearLookBack(): void {
    this.lookBackControl.setValue('');
    this.lookBackControl.updateValueAndValidity();
  }

  clearInitialQueries(): void {
    this.initialQueriesControl.setValue('');
    this.initialQueriesControl.updateValueAndValidity();
  }

  clearSupplementalQueries(): void {
    this.supplementalQueriesControl.setValue('');
    this.supplementalQueriesControl.updateValueAndValidity();
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
    if (viewOnly) {
      this.facilityIdControl.disable();
      this.planNameControl.disable();
      this.ehrDescriptionControl.disable();
      this.lookBackControl.disable();
      this.initialQueriesControl.disable();
      this.supplementalQueriesControl.disable();
    } else {
      this.facilityIdControl.enable();
      this.planNameControl.enable();
      this.ehrDescriptionControl.enable();
      this.lookBackControl.enable();
      this.initialQueriesControl.enable();
      this.supplementalQueriesControl.enable();
    }
  }


  submitConfiguration(): void {
    if (this.planForm.valid) {
      let successCount = 0;
      let errorCount = 0;
      let totalOperations = this.types.length

      for (const type of this.types) {
        if (this.formMode == FormMode.Create) {
          this.dataAcquisitionService.createQueryPlanConfiguration(this.facilityIdControl.value, {
            PlanName: this.planNameControl.value,
            FacilityId: this.facilityIdControl.value,
            EHRDescription: this.ehrDescriptionControl.value,
            LookBack: this.lookBackControl.value,
            InitialQueries: JSON.parse(this.initialQueriesControl.value),
            SupplementalQueries: JSON.parse(this.supplementalQueriesControl.value),
            Type: type
          } as IQueryPlanModel).subscribe({
            next: (response) => {
              successCount++
              if (successCount + errorCount === totalOperations) {
                this.submittedConfiguration.emit({id: '', message: `Created ${successCount} of ${totalOperations} query plans`});
              }
              //this.submittedConfiguration.emit({id: '', message: `Query Plan Created for type ${type}`});
            },
            error: (err) => {
              errorCount++;
              if (successCount + errorCount === totalOperations) {
                this.submittedConfiguration.emit({id: '', message: `Created ${successCount} of ${totalOperations} query plans. Errors: ${errorCount}`});
              }
              //this.submittedConfiguration.emit({id: '', message: err.message});
            }
          });
        } else if (this.formMode == FormMode.Edit) {
          this.dataAcquisitionService.updateQueryPlanConfiguration(this.facilityIdControl.value,
            {
              PlanName: this.planNameControl.value,
              FacilityId: this.facilityIdControl.value,
              EHRDescription: this.ehrDescriptionControl.value,
              LookBack: this.lookBackControl.value,
              InitialQueries: JSON.parse(this.initialQueriesControl.value),
              SupplementalQueries: JSON.parse(this.supplementalQueriesControl.value),
              Type: type
            } as IQueryPlanModel).subscribe({
            next: (response) => {
              //this.submittedConfiguration.emit({id: '', message: `Query Plan Updated for type ${type}`});
              successCount++
              if (successCount + errorCount === totalOperations) {
                this.submittedConfiguration.emit({id: '', message: `Updated ${successCount} of ${totalOperations} query plans`});
              }
            },
            error: (err) => {
              //this.submittedConfiguration.emit({id: '', message: err.message});
              errorCount++;
              if (successCount + errorCount === totalOperations) {
                this.submittedConfiguration.emit({id: '', message: `Updated ${successCount} of ${totalOperations} query plans. Errors: ${errorCount}`});
              }
            }
          });
        }
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
