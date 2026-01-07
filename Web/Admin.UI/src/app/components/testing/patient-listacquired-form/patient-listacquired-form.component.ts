import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {FormBuilder, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MatFormField, MatInput} from "@angular/material/input";
import {MatButton, MatIconButton} from "@angular/material/button";
import {MatIcon} from "@angular/material/icon";
import {TestService} from "../../../services/gateway/testing.service";
import {MatTooltip} from "@angular/material/tooltip";

export type ListType = 'Admit' | 'Discharge';
export type TimeFrame = 'LessThan24Hours' | 'Between24To48Hours' | 'MoreThan48Hours';

export interface PatientListItem {
  listType: ListType;
  timeFrame: TimeFrame;
  patientIds: string[];
}

@Component({
  selector: 'app-patient-lists',
  standalone: true,
  templateUrl: './patient-listacquired-form.component.html',
  imports: [
    MatFormField,
    MatInput,
    MatButton,
    MatIconButton,
    MatIcon,
    ReactiveFormsModule,
    MatTooltip
  ],
  styleUrls: ['./patient-listacquired-form.component.scss']
})
export class PatientListAcquiredComponent implements OnInit {
  @Input() facilityId = '';
  @Input() reportTrackingId = '';
  @Output() eventGenerated = new EventEmitter<string>();
  patientForm!: FormGroup;

  listTypes: ListType[] = ['Admit', 'Discharge'];
  timeFrames: TimeFrame[] = ['LessThan24Hours', 'Between24To48Hours', 'MoreThan48Hours'];

  patientLists: PatientListItem[] = [];

  constructor(private fb: FormBuilder, private snackBar: MatSnackBar, private testService: TestService,) {
  }

  ngOnInit(): void {
    this.initPatientLists();
    this.initForm();
  }

  initPatientLists() {
    this.patientLists = [];
    this.listTypes.forEach(listType => {
      this.timeFrames.forEach(timeFrame => {
        this.patientLists.push({listType, timeFrame, patientIds: []});
      });
    });
  }

  initForm() {
    const controlsConfig: any = {};
    this.patientLists.forEach((_, idx) => {
      controlsConfig[`input_${idx}`] = [''];
    });
    this.patientForm = this.fb.group(
      controlsConfig
    );
  }

  addPatients(idx: number) {
    const controlName = `input_${idx}`;
    const value = this.patientForm.get(controlName)?.value;
    if (value) {
      const patients = value
        .split(',')
        .map((p: string) => p.trim())
        .filter((p: string) => p && !this.patientLists[idx].patientIds.includes(p));
      this.patientLists[idx].patientIds.push(...patients);
      this.patientForm.get(controlName)?.reset();
    }
  }

  hasAnyPatients(): boolean {
    return this.patientLists.some(x => x.patientIds.length > 0);
  }

  removePatient(listIdx: number, patientIdx: number) {
    this.patientLists[listIdx].patientIds.splice(patientIdx, 1);
  }

  generateEvent() {
    if (this.patientForm?.valid) {
      // Flatten the patient data structure for transmission
      const patientLists = this.patientLists
        .map(item => ({
          listType: item.listType,
          timeFrame: item.timeFrame,
          patientIds: item.patientIds
        }));

      // Step 1: Generate patient acquired event for all patients
      this.testService.generatePatientListAcquiredEvent(this.facilityId, patientLists, this.reportTrackingId)
        .subscribe(data => {
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
    }
  }
}
