import { IDataAcquisitionFhirListConfigModel, IEhrPatientListModel } from '../../../interfaces/data-acquisition/data-acquisition-fhir-list-config-model.interface';
import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { FormMode } from 'src/app/models/FormMode.enum';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { ENTER, COMMA } from '@angular/cdk/keycodes';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';

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
    MatToolbarModule
  ],
  templateUrl: './data-acquisition-fhir-list-config-form.component.html',
  styleUrls: ['./data-acquisition-fhir-list-config-form.component.css']
})
export class DataAcquisitionFhirListConfigFormComponent {
  @Input() item!: IDataAcquisitionFhirListConfigModel;

  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  configForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService) {

    //initialize form with fields based on IDataAcquisitionQueryConfigModel
    this.configForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      fhirServerBaseUrl: new FormControl('', Validators.required),
      queryPlanIds: new FormControl('', Validators.required),
    });
  }

  ngOnInit(): void {
    this.configForm.reset();

    if (this.item) {
      console.log("DataAcquisitionFhirListConfigFormComponent ngOnInit");
      console.log(this.item);
      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirBaseServerUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.loadEhrPatientLists(this.item.eHRPatientLists);
      this.eHRPatientListControl.updateValueAndValidity();
    } else {
      this.formMode = FormMode.Create;
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    console.log("DataAcquisitionFhirListConfigFormComponent ngOnChanges");
    console.log(changes);
    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.fhirServerBaseUrlControl.setValue(this.item.fhirBaseServerUrl);
      this.fhirServerBaseUrlControl.updateValueAndValidity();

      this.loadEhrPatientLists(this.item.eHRPatientLists);
      this.eHRPatientListControl.updateValueAndValidity();
    }
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get fhirServerBaseUrlControl(): FormControl {
    return this.configForm.get('fhirServerBaseUrl') as FormControl;
  }

  get eHRPatientListControl(): FormArray {
    return this.configForm.get('eHRPatientList') as FormArray;
  }

  clearFacilityId(): void {
    this.facilityIdControl.setValue('');
    this.facilityIdControl.updateValueAndValidity();
  }

  clearFhirServerBaseUrl(): void {
    this.fhirServerBaseUrlControl.setValue('');
    this.fhirServerBaseUrlControl.updateValueAndValidity();
  }

  clearEHRPatientList(): void {
    this.eHRPatientListControl.setValue([]);
    this.eHRPatientListControl.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if (this.configForm.valid) {
      var queryIdArray = this.eHRPatientListControl.value.split(',');
      if (this.formMode == FormMode.Create) {
        this.dataAcquisitionService.createFhirListConfiguration(this.facilityIdControl.value, {
          facilityId: this.facilityIdControl.value,
          fhirBaseServerUrl: this.fhirServerBaseUrlControl.value,
          eHRPatientLists: this.eHRPatientListControl.value
        } as IDataAcquisitionFhirListConfigModel).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
      else if (this.formMode == FormMode.Edit) {
        this.dataAcquisitionService.createFhirListConfiguration(
          this.facilityIdControl.value,
          {
            facilityId: this.facilityIdControl.value,
            fhirBaseServerUrl: this.fhirServerBaseUrlControl.value,
            eHRPatientLists: this.eHRPatientListControl.value
          } as IDataAcquisitionFhirListConfigModel).subscribe((response: IEntityCreatedResponse) => {
            this.submittedConfiguration.emit(response);
          }
          );
      }
    }
    else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  addEHRPatientList(itemIndex: number) {
    const ehrPatientListItem = this.eHRPatientListControl.at(itemIndex);
  }

  removeEHRPatientList(itemIndex: number) {
    this.eHRPatientListControl.removeAt(itemIndex);
  }

  private loadEhrPatientLists(ehrPatientList: IEhrPatientListModel[]): void {
    console.log(ehrPatientList);
    if(ehrPatientList) {
      this.eHRPatientListControl.clear();
      this.eHRPatientListControl.updateValueAndValidity();
      ehrPatientList.forEach((ehrPatientListItem: IEhrPatientListModel) => {
      this.eHRPatientListControl.push(new FormGroup({
        measureId: new FormControl(ehrPatientListItem.measureIds.join(", "), Validators.required),
        listId: new FormControl(ehrPatientListItem.listIds.join(", "), Validators.required)
      }));
    });
    }
  }
  
}
