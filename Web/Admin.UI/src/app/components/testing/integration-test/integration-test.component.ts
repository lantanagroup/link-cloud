import {Component, OnDestroy, OnInit} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatCardModule} from '@angular/material/card';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatTabsModule} from '@angular/material/tabs';
import {MatSelectModule} from '@angular/material/select';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {TestService} from '../../../services/gateway/testing.service';
import {PaginationMetadata} from '../../../models/pagination-metadata.model';
import {EventType} from '../../../models/testing/EventType.enum';
import {animate, query, stagger, style, transition, trigger} from '@angular/animations';
import {ReportScheduledFormComponent} from '../report-scheduled-form/report-scheduled-form.component';
import {PatientAcquiredFormComponent} from "../patient-acquired-form/patient-acquired-form.component";
import {TenantService} from "../../../services/gateway/tenant/tenant.service";
import {facilityExistsValidator} from "../../validators/FacilityValidator";
import {CorrelationCacheEntry} from "../../../interfaces/testing/CorrelationCacheEntry";

const listAnimation = trigger('listAnimation', [
  transition('* <=> *', [
    query(':enter',
      [style({opacity: 0}), stagger('60ms', animate('600ms ease-out', style({opacity: 1})))],
      {optional: true}
    ),
    query(':leave',
      animate('200ms', style({opacity: 0})),
      {optional: true}
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
    ReportScheduledFormComponent,
    PatientAcquiredFormComponent
  ],
  templateUrl: './integration-test.component.html',
  styleUrls: ['./integration-test.component.scss'],
  animations: [listAnimation]
})
export class IntegrationTestComponent implements OnInit, OnDestroy {
  eventForm!: FormGroup;
  events: string[] = [EventType.REPORT_SCHEDULED, EventType.PATIENT_ACQUIRED];
  showReportScheduledForm: boolean = false;
  showPatientsAcquiredForm: boolean = false;

  correlationId: string = '';
  facilityId: string = '';
  facilities: Record<string, string> = {};
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  intervalId: ReturnType<typeof setInterval> | null | undefined;

  consumersDataOutput: Map<string, CorrelationCacheEntry[]> = new Map();
  displayedEntries: [string, any][] = [];

  isTestRunning = false;
  isLoading = false;

  reportTrackingId = '';
  panelStates: { [correlationId: string]: boolean } = {};

  constructor(
    private testService: TestService,
    private tenantService: TenantService,
    private fb: FormBuilder,
    private snackBar: MatSnackBar) {
  }

  ngOnDestroy(): void {
    this.stopPollingConsumerEvents();
  }

  ngOnInit(): void {
    this.eventForm = this.fb.group({
      facilityId: ["", [Validators.required], [facilityExistsValidator(this.tenantService)]]
    });

    // Load panel states from localStorage
    const savedStates = localStorage.getItem('panelStates');
    this.panelStates = savedStates ? JSON.parse(savedStates) : {};

    this.getFacilities().then(() => console.log('Facilities loaded'));
  }

  get facilityIdControl(): FormControl {
    return this.eventForm.get('facilityId') as FormControl;
  }

  isPanelOpen(correlationId: string): boolean {
    return this.panelStates[correlationId] || false;
  }

  togglePanel(correlationId: string, open: boolean) {
    this.panelStates[correlationId] = open;
    localStorage.setItem('panelStates', JSON.stringify(this.panelStates));
  }

  onEventGenerated(facilityId: string) {
    this.facilityId = facilityId;
    if (this.showReportScheduledForm) {
      this.showReportScheduledForm = false;
      this.showPatientsAcquiredForm = true;
    } else if (this.showPatientsAcquiredForm) {
      this.showReportScheduledForm = false;
      this.showPatientsAcquiredForm = false;
    }
  }

  createConsumers(reportTrackingId: string) {
    this.testService.startConsumers(reportTrackingId).subscribe({
      next: () => this.startPollingConsumerEvents(reportTrackingId),
      error: err => console.error('Error creating consumer:', err)
    });
  }

  deleteConsumers(facilityId: string) {
    this.testService.stopConsumers(facilityId).subscribe({
      next: () => {
        this.showReportScheduledForm = false;
        this.showPatientsAcquiredForm = false;
        this.consumersDataOutput = new Map();
        this.isLoading = false;
        this.isTestRunning = false;
      },
      error: err => {
        console.error('Error deleting consumer:', err);
        this.isTestRunning = false;
        this.isLoading = false;
      }
    });
  }

  startTest(): void {
    this.isLoading = true;
    this.facilityId = this.facilityIdControl.value;
    this.consumersDataOutput.clear();
    this.reportTrackingId = crypto.randomUUID();
    this.createConsumers(this.reportTrackingId);
    this.showReportScheduledForm = true;
    this.isTestRunning = true; // Update test state
  }

  stopTest(): void {
    this.isLoading = true;
    this.consumersDataOutput.clear();
    this.stopPollingConsumerEvents();
    this.deleteConsumers(this.reportTrackingId);
  }

  onToggleTest(): void {
    this.isTestRunning ? this.stopTest() : this.startTest();
  }

  startPollingConsumerEvents(reportScheduledId: string) {
    if (!this.intervalId) {
      this.intervalId = setInterval(() => this.pollConsumerEvents(reportScheduledId), 10000);
    }
  }

  pollConsumerEvents(reportScheduledId: string) {
    this.testService.readConsumers(reportScheduledId).subscribe({
      next: data => {
        this.consumersDataOutput.clear();

        Object.entries(data).forEach(([topic, value]) => {
          const entries: CorrelationCacheEntry[] = Array.isArray(value) ? value : JSON.parse(value || '[]');
          this.consumersDataOutput.set(topic, entries);

          // Auto-expand new errors
          entries.forEach(item => {
            if (item.errorMessage && !(item.correlationId in this.panelStates)) {
              this.panelStates[item.correlationId] = true;
            }
          });
        });

        this.displayedEntries = Array.from(this.consumersDataOutput.entries());
        localStorage.setItem('panelStates', JSON.stringify(this.panelStates));

        this.isLoading = false;
      },
      error: err => {
        console.error('Error polling consumer events:', err);
        this.isTestRunning = false;
        this.isLoading = false;

        if (this.intervalId) {
          clearInterval(this.intervalId);
          this.intervalId = null;
        }

        this.snackBar.open(err.message, '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  stopPollingConsumerEvents() {
    if (this.intervalId) {
      clearInterval(this.intervalId);
      this.intervalId = null;
      this.snackBar.open('Stopped polling consumer events', '', {
        duration: 3500,
        panelClass: 'info-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  async getFacilities() {
    this.tenantService.getAllFacilities().subscribe({
      next: facilities => this.facilities = facilities,
      error: err => console.error('Error fetching facilities:', err)
    });
  }
}
