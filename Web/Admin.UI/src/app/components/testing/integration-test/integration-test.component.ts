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
  selector: 'app-integration-test',
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
    ReportScheduledFormComponent
  ],
  templateUrl: './integration-test.component.html',
  styleUrls: ['./integration-test.component.scss'],
  animations: [listAnimation]
})
export class IntegrationTestComponent implements OnInit, OnDestroy {
  eventForm!: FormGroup;
  events: string[] = [EventType.REPORT_SCHEDULED, EventType.PATIENT_EVENT, EventType.DATA_ACQUISITION_REQUESTED];
  showReportScheduledForm: boolean = false;
  showPatientEventForm: boolean = false;
  showDataAcquisitionRequestedForm: boolean = false;

  correlationId: string = '';
  auditEvents: AuditModel[] = [];
  paginationMetadata: PaginationMetadata = new PaginationMetadata;
  intervalId!: NodeJS.Timer | null;
  isMonitoring: boolean = false;
  showProcessCard: boolean = false;

  constructor(private auditService: AuditService, private testService: TestService, private snackBar: MatSnackBar) { }

  ngOnDestroy(): void {
    this.stopPollingAuditEvents();
  }

  ngOnInit(): void {
    this.eventForm = new FormGroup({
      event: new FormControl('', Validators.required)
    });

    this.eventForm.get('event')?.valueChanges.subscribe(change => {
      switch (change) {
        case ('ReportScheduled'): {
          this.showReportScheduledForm = true;
          this.showPatientEventForm = false;
          this.showDataAcquisitionRequestedForm = false;
          break;
        }
        case ('PatientEvent'): {
          this.showReportScheduledForm = false;
          this.showPatientEventForm = true;
          this.showDataAcquisitionRequestedForm = false;
          break;
        }
        case ('DataAcquisitionRequested'): {
          this.showReportScheduledForm = false;
          this.showPatientEventForm = false;
          this.showDataAcquisitionRequestedForm = true;
          break;
        }
        default: {
          this.showReportScheduledForm = false;
          this.showPatientEventForm = false;
          this.showDataAcquisitionRequestedForm = false;
          break;
        }
      }
    });
  }

  get eventControl(): FormControl {
    return this.eventForm.get('event') as FormControl;
  }

  get currentCorrelationId(): string {
    return this.correlationId;
  }

  onEventGenerated(id: string) {
    this.correlationId = id;
    this.showProcessCard = true;
    this.startPollingAuditEvents();
  }

  startPollingAuditEvents() {
    if (this.auditEvents && this.auditEvents.length > 0) {
      this.auditEvents.splice(0, this.auditEvents.length);
    }

    this.isMonitoring = true;
    this.intervalId = setInterval( this.pollAuditEvents.bind(this), 10000); // 10 seconds in milliseconds (1000 ms = 1 second)

    this.snackBar.open('Start polling the audit event service.', '', {
      duration: 3500,
      panelClass: 'info-snackbar',
      horizontalPosition: 'end',
      verticalPosition: 'top'
    });
  }

  stopPollingAuditEvents() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
      this.isMonitoring = false;

      this.snackBar.open('Stopped polling the audit event service.', '', {
        duration: 3500,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  pollAuditEvents() {
    this.auditService.list('', '', this.correlationId, '', '', '', '', 20, 1).subscribe(data => {
      this.auditEvents = data.records;
      this.paginationMetadata = data.metadata;

      /** Testing Only */
      //let testAudit: AuditModel = {
      //  "id": this.correlationId,
      //  "facilityId": 'FACILITY_ORG_10001',
      //  "correlationId": this.correlationId,
      //  "serviceName": "Some Service",
      //  "eventDate": new Date().toUTCString(),
      //  "user": "SystemUser",
      //  "action": "Create",
      //  "resource": "SomeEntity",
      //  "propertyChanges": [],
      //  "notes": "New notification (4c05b712-0b8b-4c9a-83ab-0999120a442e) created for ''."
      //};

      //this.auditEvents.push(testAudit);

    });

  }

}
