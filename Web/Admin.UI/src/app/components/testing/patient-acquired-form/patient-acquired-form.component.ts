import {Component, EventEmitter, Input, Output} from '@angular/core';

import {MatInputModule} from '@angular/material/input';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
} from '@angular/forms';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatSelectModule} from '@angular/material/select';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatCardModule} from '@angular/material/card';
import {MatTabsModule} from '@angular/material/tabs';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatCheckboxModule} from '@angular/material/checkbox';
import {TestService} from "../../../services/gateway/testing.service";
import { MatTableModule } from '@angular/material/table';


@Component({
  selector: 'app-patient-acquired-form',
  standalone: true,
  imports: [
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
    MatCheckboxModule,
    MatTableModule
],
  templateUrl: './patient-acquired-form.component.html',
  styleUrls: ['./patient-acquired-form.component.scss']
})
export class PatientAcquiredFormComponent {

  @Input() facilityId = '';
  @Input() reportTrackingId = '';

  @Output() eventGenerated = new EventEmitter<string>();

  patientForm!: FormGroup;

  patients: string[] = []; // Array to store census patients

  dischargedPatients: string[] = []; // Array to store discharged patients

  // Columns displayed in the table
  displayedColumns: string[] = ['patientName', 'discharged', 'remove'];

  constructor(private fb: FormBuilder, private testService: TestService, private snackBar: MatSnackBar) {
    this.patientForm = this.fb.group({
      patients: [],
      dischargedPatients: [''],
    //  facilityId: new FormControl('MyFacility', Validators.required)
    });
  }

  get facilityIdControl(): FormControl {
    return this.patientForm.get('facilityId') as FormControl;
  }

  // Add patient name to array
  addPatient(): void {
    const patientNameControl = this.patientForm.get('patients');

    if (patientNameControl && patientNameControl.valid) {
      let enteredPatients = this.parseString(patientNameControl.value);

      this.patients.push(...enteredPatients); // Add to array
      patientNameControl.reset(); // Reset the input field
    }
  }

  // Remove patient name by index
  removePatient(index: number): void {
    this.patients.splice(index, 1);
  }

  // Remove patient name by index
  dischargePatient(event: any, index: number): void {
    const isChecked = event.checked; // Get the checkbox state
    console.log(`Patient at index ${index} is ${isChecked ? 'discharged' : 'not discharged'}`);
    if(isChecked) {
      this.dischargedPatients.push(this.patients[index]);
    }
    else{
      const index1 = this.dischargedPatients.findIndex(patient => patient === this.patients[index]);
      if (index1 !== -1) {
        this.dischargedPatients.splice(index1,1);
        //console.log(`Removed patient: ${patientToRemove.name} at index ${index}`);
      } else {
        console.log('Patient not found in the list');
      }

    }
  }

  parseString(patient: string): string[] {
    let enteredPatients : string[] = [];
    if (patient) {
      enteredPatients =  patient.split(',').map(item => item.trim());
    }
    return enteredPatients;
  }


  generateEvent() {
    if (this.patientForm.valid) {
      // generate patient acquired event
      this.testService.generatePatientAcquiredEvent(this.facilityId, this.patients, this.reportTrackingId).subscribe(data => {
        if (data) {

          this.eventGenerated.emit(this.facilityId);

          this.snackBar.open(data.message, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });

  setTimeout(() => {
    // Code to execute after the delay
    //generate patient acquired event
    const remainingSet = new Set(this.patients.filter(a => !this.dischargedPatients.includes(a)));
    const remainingPatients = Array.from(remainingSet);
    if (remainingPatients.length === 0) { return; }
    this.testService.generatePatientAcquiredEvent(this.facilityId, remainingPatients, this.reportTrackingId).subscribe(data => {
      if (data) {

        this.eventGenerated.emit(this.facilityId);

        this.snackBar.open(data.message, '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }, 1000);

}
else {
this.snackBar.open(`A valid patient id is required to initialize a Patient Event.`, '', {
  duration: 3500,
  panelClass: 'error-snackbar',
  horizontalPosition: 'end',
  verticalPosition: 'top'
});
}
}
}
