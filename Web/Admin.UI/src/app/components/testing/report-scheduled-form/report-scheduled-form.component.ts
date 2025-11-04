
import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {FormArray, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatCardModule} from '@angular/material/card';
import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTabsModule} from '@angular/material/tabs';
import {MatToolbarModule} from '@angular/material/toolbar';
import {IReportScheduled} from '../../../interfaces/testing/report-scheduled.interface';
import {TestService} from '../../../services/gateway/testing.service';
import {Frequency} from "../../../models/tenant/Frequency.enum";
import {MeasureDefinitionService} from "../../../services/gateway/measure-definition/measure.service";

@Component({
  selector: 'app-report-scheduled-form',
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
  templateUrl: './report-scheduled-form.component.html',
  styleUrls: ['./report-scheduled-form.component.scss']
})
export class ReportScheduledFormComponent implements OnInit {
  @Output() eventGenerated = new EventEmitter<string>();
  @Input() facilityId = '';
  @Input() reportTrackingId = '';


  eventRequestedForm!: FormGroup;
  reportTypes: string[] = [];
  frequencies: string[] = [Frequency.MONTHLY, Frequency.DAILY, Frequency.WEEKLY];
  delays: number[] = [5, 10, 15, 20, 25];

  constructor(private testService: TestService, private snackBar: MatSnackBar, private measureDefinitionConfigurationService: MeasureDefinitionService) { }

  ngOnInit(): void {

    this.measureDefinitionConfigurationService.getMeasureDefinitionConfigurations().subscribe(
      {
        next: (response) => {
          this.reportTypes = response.map(model => model.id);
        },
        error: (err) => {
          this.eventGenerated.emit(err.message);
        }
      });


    this.eventRequestedForm = new FormGroup({
      selectedReportTypes: new FormControl([], Validators.required),
      selectedFrequency: new FormControl([], Validators.required),
      selectedDelay: new FormControl([], Validators.required),
      startDate: new FormControl('', Validators.required)
     // endDate: new FormControl('', Validators.required)
    });
  }

  get reportTypeControl(): FormControl {
    return this.eventRequestedForm.get('selectedReportTypes') as FormControl;
  }

  get frequencyControl(): FormControl {
    return this.eventRequestedForm.get('selectedFrequency') as FormControl;
  }

  get delayControl(): FormControl {
    return this.eventRequestedForm.get('selectedDelay') as FormControl;
  }

  get startDateControl(): FormArray {
    return this.eventRequestedForm.get('startDate') as FormArray;
  }

  /*get endDateControl(): FormArray {
    return this.eventRequestedForm.get('endDate') as FormArray;
  }
*/
  compareReportTypes(object1: any, object2: any) {
    return (object1 && object2) && object1 === object2;
  }

  generateEvent() {
    if (this.eventRequestedForm.valid) {
      let event: IReportScheduled = this.eventRequestedForm.value;
      event.facilityId = this.facilityId; //this.facilityIdControl.value;
      event.reportTypes =   this.reportTypeControl.value;
      event.frequency = this.frequencyControl.value;
      event.delay = String(this.delayControl.value);
      event.reportTrackingId = this.reportTrackingId;
      this.testService.generateReportScheduledEvent(event.facilityId, event.reportTypes, event.frequency, event.startDate, event.delay, event.reportTrackingId).subscribe(data => {
        if (data) {

          this.eventGenerated.emit(event.facilityId);

          this.snackBar.open(data.message, '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });

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
