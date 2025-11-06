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

@Component({
  selector: 'app-query-plan-config-form',
  standalone: true,
  imports: [
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
    MatProgressSpinnerModule
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

  @Output() planSelected = new EventEmitter<any>();

  planForm: FormGroup;

  isInvalidJson = false;

  types = [
    { value: 'Discharge', label: 'Discharge' },
    { value: 'Weekly', label: 'Weekly' },
    { value: 'Daily', label: 'Daily' },
    { value: 'Monthly', label: 'Monthly' }
  ];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService, private fb: FormBuilder) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.planForm = new FormGroup({
      planName: new FormControl('', Validators.required),
      facilityId: new FormControl('', Validators.required),
      ehrDescription: new FormControl('', Validators.required),
      lookBack: new FormControl('', Validators.required),
      initialQueries: new FormControl('', [Validators.required, this.jsonValidator]),
      supplementalQueries: new FormControl('', [Validators.required, this.jsonValidator]),
      type: new FormControl('', Validators.required)
    });
  }

  ngOnInit(): void {
    this.planForm.reset();

    if (this.item) {
      //set form values
      this.planNameControl.setValue(this.item.planName);
      this.planNameControl.updateValueAndValidity();

      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.typeControl.setValue(this.item.type);
      this.typeControl.updateValueAndValidity();

      this.ehrDescriptionControl.setValue(this.item.ehrDescription);
      this.ehrDescriptionControl.updateValueAndValidity();

      this.lookBackControl.setValue(this.item.lookBack);
      this.lookBackControl.updateValueAndValidity();


      this.initialQueriesControl.setValue(this.item?.initialQueries ? JSON.stringify(this.item.initialQueries, null, 2) : '');
      this.initialQueriesControl.updateValueAndValidity();

      this.supplementalQueriesControl.setValue(this.item?.supplementalQueries ? JSON.stringify(this.item.supplementalQueries, null, 2) : '')
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

      this.planNameControl.setValue(this.item.planName);
      this.planNameControl.updateValueAndValidity();

      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.typeControl.setValue(this.item.type);
      this.typeControl.updateValueAndValidity();

      this.ehrDescriptionControl.setValue(this.item.ehrDescription);
      this.ehrDescriptionControl.updateValueAndValidity();

      this.lookBackControl.setValue(this.item.lookBack);
      this.lookBackControl.updateValueAndValidity();


      this.initialQueriesControl.setValue(this.item?.initialQueries ? JSON.stringify(this.item.initialQueries, null, 2) : '');
      this.initialQueriesControl.updateValueAndValidity();

      this.supplementalQueriesControl.setValue(this.item?.supplementalQueries ? JSON.stringify(this.item.supplementalQueries, null, 2) : '')
      this.supplementalQueriesControl.updateValueAndValidity();
    }
    // toggle view
    this.toggleViewOnly(this.viewOnly);
  }

  // Method to get the label based on the value
  getLabelFromValue(value: string): string {
    const selected = this.types.find(type => type.value === value);
    return selected ? selected.label : 'No label found';
  }

  onTypeChange(event: any) {
    console.log('Selected type:', event.value);
    this.loadQueryPlan();
  }

  loadQueryPlan() {
      this.dataAcquisitionService.getQueryPlanConfiguration(this.facilityIdControl.value, this.typeControl.value).subscribe((data: IQueryPlanModel) => {
        this.item = data;
        this.planSelected.emit({"type" : this.typeControl.value, "label": this.getLabelFromValue(this.typeControl.value), "exists" : true});
      }, error => {
        if (error.status == 404) {
          this.snackBar.open(`No current Query plan found for facility ${this.facilityIdControl.value} and type ${this.getLabelFromValue(this.typeControl.value)}, please create one.`, '', {
            duration: 5000,
            panelClass: 'info-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.item = {
            facilityId: this.facilityIdControl.value,
            planName: '',
            ehrDescription: '',
            lookBack: '',
            initialQueries: '',
            supplementalQueries: '',
            type: this.typeControl.value ?? "Discharge"
          } as IQueryPlanModel;
          this.planSelected.emit({"type" : this.typeControl.value, "label": this.getLabelFromValue(this.typeControl.value), "exists" : false});
        } else {
          this.snackBar.open(`Failed to load FHIR query plan for the facility, see error for details.`, '', {
            duration: 5000,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
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

  get typeControl(): FormControl {
    return this.planForm.get('type') as FormControl;
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
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.planNameControl.disable();
      this.ehrDescriptionControl.disable();
      this.lookBackControl.disable();
      this.initialQueriesControl.disable();
      this.supplementalQueriesControl.disable();
      this.typeControl.enable();
    } else {
      this.planNameControl.enable();
      this.ehrDescriptionControl.enable();
      this.lookBackControl.enable();
      this.initialQueriesControl.enable();
      this.supplementalQueriesControl.enable();
      this.typeControl.disable();
    }
  }


  submitConfiguration(): void {
    if (this.planForm.valid) {
        if (this.formMode == FormMode.Create) {
          this.dataAcquisitionService.createQueryPlanConfiguration(this.facilityIdControl.value, {
            PlanName: this.planNameControl.value,
            FacilityId: this.facilityIdControl.value,
            EHRDescription: this.ehrDescriptionControl.value,
            LookBack: this.lookBackControl.value,
            InitialQueries: JSON.parse(this.initialQueriesControl.value),
            SupplementalQueries: JSON.parse(this.supplementalQueriesControl.value),
            Type: this.typeControl.value
          } as any).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({id: '', message: `Created query plan`});
            },
            error: (err) => {
              this.submittedConfiguration.emit({id: '', message: `Error Creating plan`});
            }
          });
        } else if (this.formMode == FormMode.Edit) {
          this.dataAcquisitionService.updateQueryPlanConfiguration(this.facilityIdControl.value,
            {
              Id: this.item.id,
              PlanName: this.planNameControl.value,
              FacilityId: this.facilityIdControl.value,
              EHRDescription: this.ehrDescriptionControl.value,
              LookBack: this.lookBackControl.value,
              InitialQueries: JSON.parse(this.initialQueriesControl.value),
              SupplementalQueries: JSON.parse(this.supplementalQueriesControl.value),
              Type: this.typeControl.value
            } as any).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({id: '', message: `Updated query plan`});
            },
            error: (err) => {
              this.submittedConfiguration.emit({id: '', message: `Error updating query plan`});
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

}
