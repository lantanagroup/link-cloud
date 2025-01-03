import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { TestService } from '../../../services/gateway/testing.service';

@Component({
  selector: 'app-patient-event-form',
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
    MatExpansionModule
  ],
  templateUrl: './patient-event-form.component.html',
  styleUrls: ['./patient-event-form.component.scss']
})
export class PatientEventFormComponent {
  @Output() eventGenerated = new EventEmitter<string>();

  patientForm!: FormGroup;

  facilities: string[] = ['FACILITY_ORG_10001', 'FACILITY_ORG_12345'];
  eventTypes: string[] = ['Discharge'];

  constructor(private testService: TestService, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.patientForm = new FormGroup({
      facilityId: new FormControl('MyFacility', Validators.required),
      patientId: new FormControl('', Validators.required),
      eventType: new FormControl('', Validators.required)
    });
  }

  get facilityIdControl(): FormControl {
    return this.patientForm.get('facilityId') as FormControl;
  }

  get patientIdControl(): FormControl {
    return this.patientForm.get('patientId') as FormControl;
  }

  get eventTypeControl(): FormControl {
    return this.patientForm.get('eventType') as FormControl;
  }

  generateEvent() {
    if (this.patientForm.valid) {
      this.testService.generatePatientEvent(this.facilityIdControl.value, this.patientIdControl.value, this.eventTypeControl.value).subscribe(data => {
        if (data) {

          this.eventGenerated.emit(data.id);

          this.snackBar.open(data.message, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      })
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
