import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuditService } from '../../../services/gateway/audit.service';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TestService } from '../../../services/gateway/testing.service';
import { AuditModel } from '../../../models/audit/audit-model.model';
import { PaginationMetadata } from '../../../models/pagination-metadata.model';
import { EventType } from '../../../models/testing/EventType.enum';
import { DataAcquisitionReqeustedFormComponent } from '../data-acquisition-reqeusted-form/data-acquisition-reqeusted-form.component';
import { PatientEventFormComponent } from '../patient-event-form/patient-event-form.component';
import { animate, query, stagger, style, transition, trigger } from '@angular/animations';
import { ReportScheduledFormComponent } from '../report-scheduled-form/report-scheduled-form.component';
import {PatientAcquiredFormComponent} from "../patient-acquired-form/patient-acquired-form.component";
import {ReorderTopicsPipe} from "../../Pipes/ReorderTopicsPipe";


const listAnimation = trigger('listAnimation', [
  transition('* <=> *', [
    query(':enter',
      [style({ opacity: 0 }), stagger('60ms', animate('600ms ease-out', style({ opacity: 1 })))],
      { optional: true }
    ),
    query(':leave',
      animate('200ms', style({ opacity: 0 })),
      { optional: true }
    )
  ])
]);

@Component({
  selector: 'demo-integration-test',
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
    MatExpansionModule,
    MatProgressSpinnerModule,
    PatientEventFormComponent,
    DataAcquisitionReqeustedFormComponent,
    ReportScheduledFormComponent,
    PatientAcquiredFormComponent,
    ReorderTopicsPipe
  ],
  templateUrl: './integration-test.component.html',
  styleUrls: ['./integration-test.component.scss'],
  animations: [listAnimation]
})
export class IntegrationTestComponent implements OnInit, OnDestroy {
  eventForm!: FormGroup;
  events: string[] = [EventType.REPORT_SCHEDULED, EventType.PATIENT_ACQUIRED];
  showReportScheduledForm: boolean = false;
  showPatientEventForm: boolean = false;
  showDataAcquisitionRequestedForm: boolean = false;
  showPatientsAcquiredForm: boolean = false;

  correlationId: string = '';
  facilityId: string = '';
  auditEvents: AuditModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  intervalId!: NodeJS.Timer | null;

  consumersData:  Map<string, string> = new Map();

  consumersDataOutput :  Map<string, string> = new Map();

  isTestRunning = false;

  isLoading = false;

  readyToStart = false;

  constructor(private auditService: AuditService, private testService: TestService, private snackBar: MatSnackBar) { }

  ngOnDestroy(): void {
    this.stopPollingConsumerEvents();
  }

  ngOnInit(): void {
    this.eventForm = new FormGroup({
      event: new FormControl('', Validators.required),
      facilityId: new FormControl('', Validators.required)
    });
    this.readyToStart = false;
  }

  get facilityIdControl(): FormControl {
    return this.eventForm.get('facilityId') as FormControl;
  }

  get currentCorrelationId(): string {
    return this.correlationId;
  }

  onEventGenerated(facilityId: string) {
    this.facilityId = facilityId;
    if (this.showReportScheduledForm == true) {
      this.showReportScheduledForm = false;
      this.showPatientsAcquiredForm = true;
    }
    else if(this.showPatientsAcquiredForm == true){
      this.showReportScheduledForm = false;
      this.showPatientsAcquiredForm = false;
    }
  }

  createConsumers(facilityId:string) {
    this.testService.startConsumers(facilityId).subscribe(response => {
      console.log('Consumer created successfully:', response);
      this.startPollingConsumerEvents(facilityId);
    }, error => {
      console.error('Error creating consumer:', error);
    });
  }

  deleteConsumers(facilityId:string) {
    this.testService.stopConsumers(facilityId).subscribe(response => {
      this.showReportScheduledForm = false;
      this.showPatientsAcquiredForm = false;
      this.consumersDataOutput = new Map();
      this.isLoading = false; // Hide spinner
      this.isTestRunning = false; // Update test state
      this.stopPollingConsumerEvents();
    }, error => {
      console.error('Error creating consumer:', error);
    });
  }

  startTest(): void {
    this.isLoading = true; // Show spinner
    this.facilityId = this.facilityIdControl.value;
    this.isTestRunning = true; // Update test state
    this.createConsumers(this.facilityIdControl.value);
    this.showReportScheduledForm = true;
  }

  stopTest(): void {
    this.deleteConsumers(this.facilityIdControl.value);
  }

  onToggleTest(): void {
    if (this.isTestRunning) {
      // Stop the test
      this.stopTest();
    } else {
      // Start the test
      this.startTest();
    }
  }

  startPollingConsumerEvents(facilityId:string) {
    if(!this.intervalId) {
      //this.intervalId = setInterval(this.pollConsumerEvents(facilityId).bind(this), 10000); // 10 seconds in milliseconds (1000 ms = 1 second)

      this.intervalId = setInterval(() => {
        this.pollConsumerEvents(facilityId);
      }, 10000); // Poll every 10 seconds

    }
  }

  pollConsumerEvents(facilityId:string){
    this.testService.readConsumers(facilityId).subscribe(data => {
        this.consumersData = new Map(Object.entries(data));
        this.consumersData.forEach((value, key) => {
          this.consumersDataOutput.set(key, JSON.parse(value) ?? "");
        });
      //  this.consumersDataOutput = data;
        this.isLoading = false
      }
    );
  }

  stopPollingConsumerEvents() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
      this.consumersDataOutput.clear();
      this.snackBar.open('Stopped polling consumer events', '', {
        duration: 3500,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  getKeys(consumersData: { [key: string]: string }): string[] {
    return Object.keys(consumersData);
  }


}
