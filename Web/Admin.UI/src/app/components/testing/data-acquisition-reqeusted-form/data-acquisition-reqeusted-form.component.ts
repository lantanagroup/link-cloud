import { Component, EventEmitter, OnInit, Output } from '@angular/core';

import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FormArray, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { TestService } from '../../../services/gateway/testing.service';
import { IDataAcquisitionRequested } from '../../../interfaces/testing/data-acquisition-requested.interface';
import { ReportType } from '../../../models/tenant/ReportType.enum';

@Component({
  selector: 'app-data-acquisition-reqeusted-form',
  standalone: true,
  imports: [
    MatSnackBarModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatToolbarModule,
    MatCardModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule
],
  templateUrl: './data-acquisition-reqeusted-form.component.html',
  styleUrls: ['./data-acquisition-reqeusted-form.component.scss']
})
export class DataAcquisitionReqeustedFormComponent implements OnInit {
  @Output() eventGenerated = new EventEmitter<string>();

  dataRequestedForm!: FormGroup;
  facilities: string[] = ['FACILITY_ORG_10001', 'FACILITY_ORG_12345'];
  reportTypes: string[] = [ReportType.HYPO, ReportType.CDIHOB];

  constructor(private testService: TestService, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.dataRequestedForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      patientId: new FormControl('', Validators.required),
      reports: new FormArray([], Validators.required)
    });
  }

  get facilityIdControl(): FormControl {
    return this.dataRequestedForm.get('facilityId') as FormControl;
  }

  get patientIdControl(): FormControl {
    return this.dataRequestedForm.get('patientId') as FormControl;
  }

  get reportForms(): FormArray {
    return this.dataRequestedForm.get('reports') as FormArray;
  }

  addReport() {
    const reportFormGroup = new FormGroup({
      reportType: new FormControl('', Validators.required),
      startDate: new FormControl('', Validators.required),
      endDate: new FormControl('', Validators.required)
    });  
 
    this.reportForms.push(reportFormGroup);
    this.reportForms.updateValueAndValidity();
  }

  removeReport(index: number) {
    this.reportForms.removeAt(index);
    this.reportForms.updateValueAndValidity();
  }

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  generateEvent() {
    if (this.dataRequestedForm.valid) {

      let dataRequested: IDataAcquisitionRequested = this.dataRequestedForm.value;
      dataRequested.key = this.facilityIdControl.value;

      this.testService.generateDataAcquisitionRequestedEvent(dataRequested.key, dataRequested.patientId, dataRequested.reports).subscribe(data => {
        if (data) {

          this.eventGenerated.emit(data.id);

          this.snackBar.open(data.message, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });

      //*** Testing Only */
      //this.eventGenerated.emit("33f0f120-9aa5-4d2d-91dc-c97b1ba68bc9");
      //this.snackBar.open('Testing', '', {
      //  duration: 3500,
      //  panelClass: 'success-snackbar',
      //  horizontalPosition: 'end',
      //  verticalPosition: 'top'
      //});
    }
    else {
      this.snackBar.open(`All required fields must be entered to create a data acquisition requested event.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

}
