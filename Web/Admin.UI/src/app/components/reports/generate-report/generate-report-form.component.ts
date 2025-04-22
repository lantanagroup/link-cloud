import {Component, EventEmitter, OnDestroy, OnInit, Output} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import {CommonModule} from "@angular/common";
import {MatSnackBar, MatSnackBarModule} from "@angular/material/snack-bar";
import {MatFormFieldModule} from "@angular/material/form-field";
import {MatInputModule} from "@angular/material/input";
import {MatSelectModule} from "@angular/material/select";
import {MatToolbarModule} from "@angular/material/toolbar";
import {MatCardModule} from "@angular/material/card";
import {MatTabsModule} from "@angular/material/tabs";
import {MatButtonModule} from "@angular/material/button";
import {MatIconModule} from "@angular/material/icon";
import {MatExpansionModule} from "@angular/material/expansion";
import {MatProgressSpinnerModule} from "@angular/material/progress-spinner";
import {MatDatepickerModule} from "@angular/material/datepicker";
import {
  IAdHocReportRequest,
  IFacilityConfigModel
} from "../../../interfaces/tenant/facility-config-model.interface";
import {TenantService} from "../../../services/gateway/tenant/tenant.service";
import {MeasureDefinitionService} from "../../../services/gateway/measure-definition/measure.service";
import {PatientAcquiredFormComponent} from "../../testing/patient-acquired-form/patient-acquired-form.component";
import {IMeasureDefinitionConfigModel} from "../../../interfaces/measure-definition/measure-definition-config-model.interface";
import {IEntityCreatedResponse} from "../../../interfaces/entity-created-response.model";
import {forkJoin, Observable} from "rxjs";
import {MatCheckboxModule} from "@angular/material/checkbox";
import {MatRadioModule} from "@angular/material/radio";
import * as Papa from 'papaparse';
import {FileUploadComponent} from "../../core/file-upload/file-upload.component";

@Component({
  selector: 'generate-report-form',
  standalone: true,
  imports: [
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
    MatCheckboxModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    MatDatepickerModule,
    PatientAcquiredFormComponent,
    MatRadioModule,
    FileUploadComponent
  ],
  templateUrl: './generate-report-form.component.html',
  styleUrls: ['./generate-report-form.component.scss']
})
export class GenerateReportFormComponent {

  generateReportForm: FormGroup;
  facilities: IFacilityConfigModel[] = [];
  reportTypes: string[] = [];
  facilityId: string = '';
  patients: string[] = [];
  formSubmitted = false; // Flag to track form submission
  errorMessage: string = '';

  @Output() formValueChanged = new EventEmitter<boolean>();

  constructor(private fb: FormBuilder, private tenantService: TenantService, private measureDefinitionConfigurationService: MeasureDefinitionService, private snackBar: MatSnackBar) {
    this.generateReportForm = this.fb.group({
      facilityId: ['', Validators.required],
      bypassSubmission: [false],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      reportTypes: ['', Validators.required],
      patients: [],
      selectedForm: ['fileUpload']
    });
  }

  ngOnInit(): void {
    forkJoin([this.getReportTypes(), this.getFacilities()]).subscribe({
      next: ([reportTypes, facilities]) => {
        this.facilities = facilities.records;
        this.reportTypes = reportTypes.map(model => model.id);
      },
      error: (error) => {
        console.error('Error fetching data:', error);
      }
    });

    this.generateReportForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.generateReportForm.invalid);
    });
  }

  get selectedFormControl(): FormControl {
    return this.generateReportForm.get('selectedForm') as FormControl;
  }


  get facilityIdControl(): FormControl {
    return this.generateReportForm.get('facilityId') as FormControl;
  }

  get startDateControl(): FormArray {
    return this.generateReportForm.get('startDate') as FormArray;
  }

  get endDateControl(): FormArray {
    return this.generateReportForm.get('endDate') as FormArray;
  }

  get bypassSubmissionControl(): FormControl {
    return this.generateReportForm.get('bypassSubmission') as FormControl;
  }

  get reportTypesControl(): FormControl {
    return this.generateReportForm.get('reportTypes') as FormControl;
  }

  get patientIdsControl(): FormControl {
    return this.generateReportForm.get('patientIds') as FormControl;
  }

  // Remove patient name by index
  removePatient(index: number): void {
    this.patients.splice(index, 1);
  }

  generateReport() {
    this.formSubmitted = true; // Set flag when form is submitted
    if (this.generateReportForm.valid) {
      console.log('Report Data:', this.generateReportForm.value);
      let adHocReportRequest: IAdHocReportRequest = {
        'bypassSubmission': this.bypassSubmissionControl.value,
        'startDate': this.startDateControl.value,
        'endDate': this.endDateControl.value,
        'reportTypes': this.reportTypesControl.value,
        'patientIds': this.patients
      };
      this.tenantService.generateAdHocReport(this.facilityIdControl.value, adHocReportRequest).subscribe((response: IEntityCreatedResponse) => {
        this.snackBar.open(`Successfully generated report.`, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.resetForm();

      });
    } else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 2500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'center',
        verticalPosition: 'top'
      });
    }
  }

  private resetForm() {
    this.patients = [];
    this.generateReportForm.controls['startDate'].reset();
    this.generateReportForm.controls['endDate'].reset();
    this.generateReportForm.controls['facilityId'].reset();
    this.generateReportForm.controls['reportTypes'].reset();
    this.generateReportForm.controls['patients'].reset();
    this.generateReportForm.controls['bypassSubmission'].reset();
  }

  getReportTypes(): Observable<IMeasureDefinitionConfigModel[]> {
    return this.measureDefinitionConfigurationService.getMeasureDefinitionConfigurations();
  }

  getFacilities() {
    return this.tenantService.listFacilities('', '', "facilityId", 0, 1000, 0);
  }

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  // Add patient name to array
  addPatient(): void {
    const patientNameControl = this.generateReportForm.get('patients');
    if (patientNameControl && patientNameControl.valid) {
      if (patientNameControl?.valid) {
        let enteredPatients = this.parseString(patientNameControl.value);
        // Filter out duplicates before adding to the array
        const newPatients = enteredPatients.filter(patient =>
          !this.patients.includes(patient) && patient.length > 0
        );
        if (newPatients.length > 0) {
          this.patients.push(...newPatients); // Add to array
        } else if (enteredPatients.length > 0 && newPatients.length === 0) {
          this.snackBar.open('All patient IDs already added', '', {
            duration: 2000,
            horizontalPosition: 'center'
          });
        }
        patientNameControl.reset(); // Reset the input field
      }
    }
  }

  loadFile(file: any) {
    const reader = new FileReader();
    reader.readAsText(file);
    const fileName = file.name.toLowerCase();
    if (fileName.endsWith('.csv')) {
      this.errorMessage = '';
    } else {
      this.errorMessage = 'Please upload a valid CSV file (with .csv extension).';
      return;
    }
    reader.onload = () => {
      const csvData = reader.result as string;
      Papa.parse(csvData, {
        header: false,
        skipEmptyLines: true,
        complete: (result) => {
          this.patients = (result.data as string[][]).map(row => row[0]);
        },
      });
    };
    reader.onerror = () => {
      throw new Error('Error reading the file.');
    };
  }

  parseString(patient: string): string[] {
    let enteredPatients: string[] = [];
    if (patient) {
      enteredPatients = patient.split(',').map(item => item.trim());
    }
    return enteredPatients;
  }

}
