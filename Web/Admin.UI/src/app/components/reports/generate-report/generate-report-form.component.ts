import {AfterViewInit, Component, EventEmitter, OnDestroy, OnInit, Output, ViewChild} from '@angular/core';
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
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
import {MatTableDataSource, MatTableModule} from "@angular/material/table";
import {MatPaginator, MatPaginatorModule} from "@angular/material/paginator";
import {MatDatepickerModule} from "@angular/material/datepicker";
import {IAdHocReportRequest} from "../../../interfaces/tenant/facility-config-model.interface";
import {TenantService} from "../../../services/gateway/tenant/tenant.service";
import {MeasureDefinitionService} from "../../../services/gateway/measure-definition/measure.service";
import {
  IMeasureDefinitionConfigModel
} from "../../../interfaces/measure-definition/measure-definition-config-model.interface";
import {IReportGenerationResponse} from "../../../interfaces/entity-created-response.model";
import {
  debounceTime,
  distinctUntilChanged,
  firstValueFrom,
  map,
  Observable,
  of,
  startWith,
  Subject,
  takeUntil,
  tap
} from "rxjs";
import {MatCheckboxModule} from "@angular/material/checkbox";
import {MatRadioModule} from "@angular/material/radio";
import * as Papa from 'papaparse';
import {FileUploadComponent} from "../../core/file-upload/file-upload.component";
import {Router} from '@angular/router';
import {facilityExistsValidator} from "../../validators/FacilityValidator";
import {switchMap} from "rxjs/operators";
import {FaIconComponent} from "@fortawesome/angular-fontawesome";
import {MatAutocomplete, MatAutocompleteTrigger} from "@angular/material/autocomplete";
import {faSearch} from "@fortawesome/free-solid-svg-icons";
import {fromZonedTime} from 'date-fns-tz';
import {TextFieldModule} from '@angular/cdk/text-field';


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
    MatRadioModule,
    FileUploadComponent,
    FaIconComponent,
    MatAutocompleteTrigger,
    MatAutocomplete,
    MatTableModule,
    MatPaginatorModule,
    TextFieldModule
  ],
  templateUrl: './generate-report-form.component.html',
  styleUrls: ['./generate-report-form.component.scss']
})
export class GenerateReportFormComponent implements OnInit, OnDestroy, AfterViewInit {

  generateReportForm: FormGroup;
  facilities: Array<{ facilityId: string, facilityName: string }> = [];
  reportTypes: string[] = [];
  patients: string[] = [];
  dataSource = new MatTableDataSource<string>();
  displayedColumns = ['patient', 'delete'];
  searchFilter = '';
  isFileLoading = false;
  formSubmitted = false; // Flag to track form submission
  errorMessage: string = '';
  lastGeneratedReport: { facilityId: string, reportId: string } | null = null;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  private destroy$ = new Subject<void>();

  @Output() formValueChanged = new EventEmitter<boolean>();

  filteredFacilities: Observable<{ facilityId: string; facilityName: string }[]> = of([]);

  constructor(
    private fb: FormBuilder,
    private tenantService: TenantService,
    private measureDefinitionConfigurationService: MeasureDefinitionService,
    private snackBar: MatSnackBar,
    private router: Router) {
    this.generateReportForm = this.fb.group({
      facilityInput: [''], // typed text
      facilityId: this.fb.control('', {
        validators: [Validators.required],
        asyncValidators: [facilityExistsValidator(this.tenantService)],
        updateOn: 'blur'
      }),
      bypassSubmission: [false],
      reportingCadence: ['Custom'],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      reportTypes: ['', Validators.required],
      patients: [],
      selectedForm: ['manualPatients']
    });
  }

  ngOnInit() {
    // Load report types only
    this.getReportTypes().subscribe(reportTypes => {
      this.reportTypes = reportTypes.map(r => r.id);
    });

    this.filteredFacilities = this.facilityInputControl.valueChanges.pipe(
      startWith(''),
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => this.tenantService.autocompleteFacilities(term || '')),
      map(results => Object.entries(results || {}).map(([facilityId, facilityName]) => ({facilityId, facilityName}))),
      tap(facilities => (this.facilities = facilities))
    );

    this.reportingCadenceControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(cadence => {
      if (cadence === 'Daily') {
        this.endDateControl.disable();
        if (this.startDateControl.value) {
          this.endDateControl.setValue(this.startDateControl.value);
        }
      } else if (cadence === 'Monthly') {
        this.endDateControl.disable();
      } else {
        this.endDateControl.enable();
      }
    });

    this.startDateControl.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(startDate => {
      const cadence = this.reportingCadenceControl.value;
      if (cadence === 'Daily') {
        this.endDateControl.setValue(startDate);
      }
    });

    this.generateReportForm.valueChanges.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.formValueChanged.emit(this.generateReportForm.invalid);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.filterPredicate = (data: string, filter: string) => {
      const terms = filter.split(',').map(t => t.trim()).filter(t => t.length > 0);
      return terms.length === 0 || terms.some(term => data.toLowerCase().includes(term));
    };
  }

  applyFilter(filterValue: string): void {
    this.searchFilter = filterValue;
    this.dataSource.filter = filterValue.trim().toLowerCase();
  }

  clearFilter(): void {
    this.searchFilter = '';
    this.dataSource.filter = '';
  }

  onFacilityInputBlur() {
    setTimeout(() => {
      const inputVal = this.facilityInputControl.value?.trim();
      const facilityIdControl = this.facilityIdControl;

      if (!inputVal) {
        facilityIdControl.setValue('');
      } else {
        facilityIdControl.setValue(inputVal);
      }

      facilityIdControl.markAsTouched();
      facilityIdControl.updateValueAndValidity();
    }, 200);
  }


  onFacilitySelected(selectedValue: { facilityId: string; facilityName: string }) {
    const facility = this.facilities.find(f => f.facilityId === selectedValue.facilityId);
    if (facility) {
      this.facilityIdControl.setValue(facility.facilityId);
      this.facilityInputControl.setValue(facility.facilityId);
      this.facilityIdControl.setErrors(null);
    }
  }

  clearSearchInput() {
    this.facilityInputControl.setValue('');
    this.facilityIdControl.setValue('');
  }

  get selectedFormControl(): FormControl {
    return this.generateReportForm.get('selectedForm') as FormControl;
  }

  get facilityIdControl(): FormControl {
    return this.generateReportForm.get('facilityId') as FormControl;
  }

  get facilityInputControl(): FormControl {
    return this.generateReportForm.get('facilityInput') as FormControl;
  }

  get reportingCadenceControl(): FormControl {
    return this.generateReportForm.get('reportingCadence') as FormControl;
  }

  get startDateControl(): FormControl {
    return this.generateReportForm.get('startDate') as FormControl;
  }

  get endDateControl(): FormControl {
    return this.generateReportForm.get('endDate') as FormControl;
  }

  get bypassSubmissionControl(): FormControl {
    return this.generateReportForm.get('bypassSubmission') as FormControl;
  }

  get reportTypesControl(): FormControl {
    return this.generateReportForm.get('reportTypes') as FormControl;
  }

  clearAllPatients(): void {
    this.patients = [];
    this.dataSource.data = [];
    this.clearFilter();
  }

  // Remove patient by ID value (index-independent so it works with filtered/paginated views)
  removePatient(patientId: string): void {
    const idx = this.patients.indexOf(patientId);
    if (idx !== -1) {
      this.patients.splice(idx, 1);
      this.dataSource.data = [...this.patients];
    }
  }

  toNoTimeZoneDateString(date: Date): string {
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
  }

  async generateReport() {
    this.formSubmitted = true; // Set flag when form is submitted
    if (this.generateReportForm.valid) {

      // Wait for the facility configuration
      const config = await firstValueFrom(this.tenantService.getFacilityConfiguration(this.facilityIdControl.value));
      const facilityTimeZone = config.timeZone || 'UTC'; // fallback if not set

      const noTimeZoneStart = this.toNoTimeZoneDateString(this.startDateControl.value);
      // fromZonedTime treats noTimeZoneStart as if it is in the facility timezone
      const startUtc = fromZonedTime(new Date(noTimeZoneStart), facilityTimeZone);

      // End of day
      const noTimeZoneEnd = this.toNoTimeZoneDateString(this.endDateControl.value);
      const endDate = new Date(noTimeZoneEnd);
      endDate.setHours(23, 59, 59, 999);
      // fromZonedTime treats noTimeZoneEnd as if it is in the facility timezone
      const endUtc = fromZonedTime(endDate, facilityTimeZone);

      let adHocReportRequest: IAdHocReportRequest = {
        'bypassSubmission': this.bypassSubmissionControl.value,
        'startDate': startUtc,
        'endDate': endUtc,
        'reportTypes': this.reportTypesControl.value,
        'patientIds': this.selectedFormControl.value === 'censusPatients' ? [] : this.patients
      };
      this.tenantService.generateAdHocReport(this.facilityIdControl.value, adHocReportRequest).subscribe({
        next: (response: IReportGenerationResponse) => {
          this.snackBar.open(`Successfully generated report with ID: ${response.reportId}.`, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.lastGeneratedReport = {facilityId: this.facilityIdControl.value, reportId: response.reportId};
          this.router.navigate([`tenant/facility/${this.facilityIdControl.value}/report/${response.reportId}`]);
        },
        error: (err) => {
          // Display error message
          this.snackBar.open('Failed to generate report. Please try again.', '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
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
    this.dataSource.data = [];
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

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  chosenMonthHandler(normalizedMonth: Date, datepicker: any) {
    if (this.reportingCadenceControl.value !== 'Monthly') {
      return;
    }
    const ctrlValue = new Date();
    ctrlValue.setFullYear(normalizedMonth.getFullYear());
    ctrlValue.setMonth(normalizedMonth.getMonth());
    ctrlValue.setDate(1);
    ctrlValue.setHours(0, 0, 0, 0);
    this.startDateControl.setValue(ctrlValue);

    const endValue = new Date(ctrlValue);
    endValue.setMonth(ctrlValue.getMonth() + 1);
    endValue.setDate(0);
    endValue.setHours(0, 0, 0, 0);
    this.endDateControl.setValue(endValue);

    datepicker.close();
  }

  // Add patient name to array
  addPatient(): void {
    const patientNameControl = this.generateReportForm.get('patients');
    if (patientNameControl && patientNameControl.valid) {
      if (patientNameControl?.valid) {
        let enteredPatients = this.parseString(patientNameControl.value);
        // Filter out duplicates before adding to the array
        const newPatients = [...new Set(enteredPatients.filter(patient =>
          !this.patients.includes(patient) && patient.length > 0
        ))];
        if (newPatients.length > 0) {
          this.patients.push(...newPatients); // Add to array
          this.dataSource.data = [...this.patients];
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
    const fileName = file.name.toLowerCase();
    if (fileName.endsWith('.csv')) {
      this.errorMessage = '';
    } else {
      this.errorMessage = 'Please upload a valid CSV file (with .csv extension).';
      return;
    }
    this.isFileLoading = true;
    reader.readAsText(file);
    reader.onload = () => {
      const csvData = reader.result as string;
      Papa.parse(csvData, {
        header: false,
        skipEmptyLines: true,
        complete: (result) => {
          const rawIds = (result.data as string[][]).map(row => row[0].trim()).filter(id => id.length > 0);
          this.patients = [...new Set(rawIds)];
          this.dataSource.data = [...this.patients];
          this.isFileLoading = false;
        },
      });
    };
    reader.onerror = () => {
      this.isFileLoading = false;
      throw new Error('Error reading the file.');
    };
  }

  parseString(patient: string): string[] {
    if (!patient) return [];
    return patient.split(/[\n,]/).map(item => item.trim()).filter(item => item.length > 0);
  }

  navToReport() {
    if (this.lastGeneratedReport?.reportId) {
      this.router.navigate([`tenant/facility/${this.lastGeneratedReport.facilityId}/report/${this.lastGeneratedReport.reportId}`]);
    }
  }

  get canGenerateReport(): boolean {
    if (this.generateReportForm.invalid || this.isFileLoading) {
      return false;
    }

    const selectedForm = this.selectedFormControl.value;
    const hasPatients = this.patients && this.patients.length > 0;

    if (selectedForm === 'censusPatients') {
      return true;
    }

    if (selectedForm === 'fileUpload' || selectedForm === 'manualPatients') {
      return hasPatients;
    }

    return false;
  }

  protected readonly faSearch = faSearch;

}
