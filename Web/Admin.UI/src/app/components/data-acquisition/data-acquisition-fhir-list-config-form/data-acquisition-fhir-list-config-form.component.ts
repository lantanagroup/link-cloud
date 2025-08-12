import {
  IDataAcquisitionFhirListConfigModel,
  IEhrPatientListModel
} from '../../../interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatToolbarModule} from '@angular/material/toolbar';
import {FormMode} from 'src/app/models/FormMode.enum';
import {IEntityCreatedResponse} from 'src/app/interfaces/entity-created-response.model';
import {ENTER, COMMA} from '@angular/cdk/keycodes';
import {DataAcquisitionService} from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {MatSelectModule} from "@angular/material/select";
import {MeasureDefinitionService} from "../../../services/gateway/measure-definition/measure.service";

@Component({
  selector: 'app-data-acquisition-fhir-list-config-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    MatSelectModule,
    ReactiveFormsModule,
    MatSelectModule
  ],
  templateUrl: './data-acquisition-fhir-list-config-form.component.html',
  styleUrls: ['./data-acquisition-fhir-list-config-form.component.scss']
})
export class DataAcquisitionFhirListConfigFormComponent {
  @Input() item!: IDataAcquisitionFhirListConfigModel;

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

  configForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  reportTypes: string[] = [];

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService, private measureDefinitionConfigurationService: MeasureDefinitionService, private fb: FormBuilder) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.configForm = this.fb.group({
      facilityId: this.fb.control('', Validators.required),
      fhirServerBaseUrl: this.fb.control('', Validators.required),
      patientListControl: this.fb.array([ // Initialize FormArray with an array of FormGroups
        this.fb.group({
          listIds: ['', Validators.required],
          measureIds: ['', Validators.required]
        })
      ])
    });
  }

  ngOnInit(): void {
    this.configForm.reset();

    this.measureDefinitionConfigurationService.getMeasureDefinitionConfigurations().subscribe(
      {
        next: (response) => {
          this.reportTypes = response.map(model => model.id);
        },
        error: (err) => {
          this.submittedConfiguration.emit({id: '', message: err.message});
        }
      });

    if (this.item) {
      console.log("DataAcquisitionFhirListConfigFormComponent ngOnInit");
      console.log(this.item);
      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirBaseServerUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.loadPatientLists(this.item.ehrPatientLists);
      this.patientListControl.updateValueAndValidity();

    } else {
      this.formMode = FormMode.Create;
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirBaseServerUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.loadPatientLists(this.item.ehrPatientLists);
      this.patientListControl.updateValueAndValidity();

      // toggle view
      this.toggleViewOnly(this.viewOnly);
    }
  }

  // Dynamically disable or enable the form control based on viewOnly
  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.fhirServerBaseUrlControl.disable();
      this.patientListControl.disable();
    } else {
      this.fhirServerBaseUrlControl.enable();
      this.patientListControl.enable();
    }
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get fhirServerBaseUrlControl(): FormControl {
    return this.configForm.get('fhirServerBaseUrl') as FormControl;
  }

  get patientListControl() {
    return this.configForm.get('patientListControl') as FormArray;
  }

  clearFhirServerBaseUrl(): void {
    this.fhirServerBaseUrlControl.setValue('');
    this.fhirServerBaseUrlControl.updateValueAndValidity();
  }

  clearPatientList(): void {
    this.patientListControl.setValue([]);
    this.patientListControl.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if (this.configForm.valid) {
      const ehrPatientLists = this.patientListControl.controls.map((control, index) => {
        const patientForm = control as FormGroup;
        return {
          measureIds: patientForm.value.measureIds,
          listIds: patientForm.value.listIds
            ? patientForm.value.listIds.split(',')
            : []
        };
      });
      if (this.formMode == FormMode.Create) {
        this.dataAcquisitionService.createFhirListConfiguration(this.facilityIdControl.value, {
          facilityId: this.facilityIdControl.value,
          fhirBaseServerUrl: this.fhirServerBaseUrlControl.value,
          ehrPatientLists: ehrPatientLists
        } as IDataAcquisitionFhirListConfigModel).subscribe({
          next: (response) => {
            this.submittedConfiguration.emit({id: response.id, message: "Patient List Created"});
          },
          error: (err) => {
            this.submittedConfiguration.emit({id: this.item.id ?? '', message: err.message});
          }
        });
      } else if (this.formMode == FormMode.Edit) {
        this.dataAcquisitionService.updateFhirListConfiguration(
          this.facilityIdControl.value,
          {
            facilityId: this.facilityIdControl.value,
            fhirBaseServerUrl: this.fhirServerBaseUrlControl.value,
            ehrPatientLists: ehrPatientLists
          } as IDataAcquisitionFhirListConfigModel).subscribe({
            next: (response) => {
              this.submittedConfiguration.emit({id: response.id, message: "Patient List Updated"});
            },
            error: (err) => {
              this.submittedConfiguration.emit({id: this.item.id ?? '', message: err.message});
            }
          }
        );
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

  addPatientList(itemIndex: number) {
    const patientForm = this.fb.group({
      measureIds: this.fb.control('', Validators.required),
      listIds: this.fb.control('', Validators.required)
    });
    this.patientListControl.push(patientForm);
  }

  removePatientList(itemIndex: number) {
    this.patientListControl.removeAt(itemIndex);
  }

  private loadPatientLists(ehrPatientList: IEhrPatientListModel[]): void {

    this.patientListControl.clear();
    this.patientListControl.updateValueAndValidity();

    if (ehrPatientList?.length) {

      ehrPatientList.forEach((ehrPatientListItem: IEhrPatientListModel) => {

        let measureIds = (ehrPatientListItem.measureIds ?? []);
        let listIds = (ehrPatientListItem.listIds ?? []).join(", ");

        const patientForm = this.fb.group({
          measureIds: this.fb.control(measureIds, Validators.required),
          listIds: this.fb.control(listIds, Validators.required)
        });
        this.patientListControl.push(patientForm);
      });
    } else {
      const patientForm = this.fb.group({
        measureIds: this.fb.control('', Validators.required),
        listIds: this.fb.control('', Validators.required)
      });
      this.patientListControl.push(patientForm);
    }

  }

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

}
