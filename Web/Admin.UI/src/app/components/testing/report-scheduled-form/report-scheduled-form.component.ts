import { CommonModule } from '@angular/common';
import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { FormArray, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { IReportScheduled } from '../../../interfaces/testing/report-scheduled.interface';
import { ReportType } from '../../../models/tenant/ReportType.enum';
import { TestService } from '../../../services/gateway/testing.service';

@Component({
  selector: 'app-report-scheduled-form',
  standalone: true,
  imports: [
    CommonModule,
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
  templateUrl: './report-scheduled-form.component.html',
  styleUrls: ['./report-scheduled-form.component.scss']
})
export class ReportScheduledFormComponent implements OnInit {
  @Output() eventGenerated = new EventEmitter<string>();

  eventRequestedForm!: FormGroup;
  reportTypes: string[] = [ReportType.HYPO, ReportType.CDIHOB];

  constructor(private testService: TestService, private snackBar: MatSnackBar) { }

  ngOnInit(): void {
    this.eventRequestedForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      reportType: new FormControl('', Validators.required),
      startDate: new FormControl('', Validators.required),
      endDate: new FormControl('', Validators.required)
    });
  }

  get facilityIdControl(): FormControl {
    return this.eventRequestedForm.get('facilityId') as FormControl;
  }

  get reportTypeControl(): FormControl {
    return this.eventRequestedForm.get('reportType') as FormControl;
  }

  get startDateControl(): FormArray {
    return this.eventRequestedForm.get('startDate') as FormArray;
  }

  get endDateControl(): FormArray {
    return this.eventRequestedForm.get('endDate') as FormArray;
  }

  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  generateEvent() {
    if (this.eventRequestedForm.valid) {

      let event: IReportScheduled = this.eventRequestedForm.value;
      event.facilityId = this.facilityIdControl.value;

      this.testService.generateReportScheduledEvent(event.facilityId, event.reportType, event.startDate, event.endDate).subscribe(data => {
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
      this.snackBar.open(`All required fields must be entered to create a report scheduled event.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }
}
