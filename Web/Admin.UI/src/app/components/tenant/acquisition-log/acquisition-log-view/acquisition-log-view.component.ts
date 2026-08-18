import {animate, keyframes, style, transition, trigger} from '@angular/animations';
import {CommonModule} from '@angular/common';
import {Component, OnInit} from '@angular/core';
import {AcquisitionLogSummary} from '../models/acquisition-log-summary';
import {AcquisitionLogService} from '../acquisition-log.service';
import {FontAwesomeModule} from '@fortawesome/angular-fontawesome';
import {
  faArrowLeft,
  faBan,
  faArrowRotateLeft,
  faFilter,
  faPlus,
  faRotate,
  faSort,
  faSortDown,
  faSortUp,
  faXmark
} from '@fortawesome/free-solid-svg-icons';
import {PaginationMetadata} from 'src/app/models/pagination-metadata.model';
import {MatPaginatorModule, PageEvent} from '@angular/material/paginator';
import {FormsModule} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatSelectModule} from '@angular/material/select';
import {MatCheckboxModule} from '@angular/material/checkbox';
import {MatTooltipModule} from '@angular/material/tooltip';
import {LoadingService} from 'src/app/services/loading.service';
import {finalize, forkJoin} from 'rxjs';
import {TenantService} from 'src/app/services/gateway/tenant/tenant.service';
import {MatDialogModule} from '@angular/material/dialog';
import {MatSnackBar} from '@angular/material/snack-bar';
import {SnackbarHelper} from 'src/app/services/snackbar-helper';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatAutocompleteModule} from '@angular/material/autocomplete';
import {MatExpansionModule} from '@angular/material/expansion';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {TableCommandComponent} from "./table-command/table-command.component";
import {ReportService} from 'src/app/services/gateway/report/report.service';
import {PieChartComponent} from 'src/app/components/core/pie-chart/pie-chart.component';
import {
  IDataAcquisitionLogStatusCount,
  IDataAcquisitionLogStatusStatistics
} from 'src/app/interfaces/data-acquisition/data-acquisition-log-status-statistics.interface';

@Component({
  selector: 'app-acquisition-log-view',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    FontAwesomeModule,
    MatPaginatorModule,
    MatDialogModule,
    TableCommandComponent,
    PieChartComponent,
    MatSelectModule,
    MatCheckboxModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    MatExpansionModule,
    RouterLink
],
  templateUrl: './acquisition-log-view.component.html',
  styleUrl: './acquisition-log-view.component.scss',
  animations: [
    trigger('fadeInSlideUp', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(10px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ]),
    trigger('fadeGrowRightOut', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scaleX(0.5) scaleY(0.8) translateX(40px) translateY(10px)' }),
        animate('250ms cubic-bezier(.4,0,.2,1)', style({ opacity: 1, transform: 'scaleX(1) scaleY(1) translateX(0) translateY(0)' }))
      ])
    ]),
    trigger('fadeInOutScale', [
      transition(':enter', [
        animate(
          '600ms cubic-bezier(.23,1.02,.57,1.01)',
          keyframes([
            style({ opacity: 0, transform: 'scale3d(.9, .9, .9)', offset: 0 }),
            style({ opacity: 1, transform: 'scale3d(1.1, 1.1, 1.1)', offset: 0.4 }),
            style({ transform: 'scale3d(0.95, 0.95, 0.95)', offset: 0.6 }),
            style({ transform: 'scale3d(1.02, 1.02, 1.02)', offset: 0.8 }),
            style({ opacity: 1, transform: 'scale3d(1, 1, 1)', offset: 1 })
          ])
        )
      ]),
      transition(':leave', [
        animate(
          '100ms cubic-bezier(.4,0,.2,1)',
          style({ opacity: 0, transform: 'scale3d(.9, .9, .9)' })
        )
      ])
    ])
  ]
})
export class AcquisitionLogViewComponent implements OnInit {
  // All resource types supported by Epic and Cerner as of 2/10/26
  private readonly resourceTypes: string[] = [
    'AllergyIntolerance', 'Appointment', 'AppointmentResponse', 'AuditEvent', 'Binary', 'CarePlan', 'CareTeam',
    'Condition', 'Consent', 'Coverage', 'Device', 'DeviceRequest', 'DeviceUseStatement', 'DiagnosticReport',
    'DocumentReference', 'Encounter', 'EpisodeOfCare', 'Goal', 'Group', 'Immunization', 'ImmunizationRecommendation',
    'Location', 'Medication', 'MedicationAdministration', 'MedicationRequest', 'MedicationStatement', 'Observation',
    'Organization', 'Patient', 'Person', 'Practitioner', 'PractitionerRole', 'Procedure', 'Provenance',
    'Questionnaire', 'QuestionnaireResponse', 'ReferralRequest', 'ServiceRequest', 'RelatedPerson', 'Schedule',
    'SearchParameter', 'Slot', 'Specimen', 'StructureDefinition', 'Subscription', 'ValueSet', 'CodeSystem'
  ];

  faXmark = faXmark;
  faRotate = faRotate;
  faArrowLeft = faArrowLeft;
  faFilter = faFilter;
  faPlus = faPlus;
  faSort = faSort;
  faSortUp = faSortUp;
  faSortDown = faSortDown;
  faBan = faBan;
  faArrowRotateLeft = faArrowRotateLeft;
  private readonly PAGE_SIZE_KEY = 'acquisitionLogPageSize';

  defaultPageNumber: number = 0
  defaultPageSize: number = 10;
  sortBy: string | null = null;
  sortOrder: 'ascending' | 'descending' | null = null;
  acquisitionLogs: AcquisitionLogSummary[] = [];
  animatedRows = new Set<string>();
  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  //filters
  allowLogSelection: boolean = false;
  patientFilter: string = '';
  resourceIdFilter: string = '';
  reportIdFilter: string = '';
  reportIdFromRoute: string = '';
  reportFacilityId: string = '';
  patientIdFromRoute: string = '';
  facilityFilterOptions: Record<string, string> = {};
  selectedFacilityFilter: string = 'Any';
  resourceTypeFilterOptions: string[] = [];
  filteredResourceTypeOptions: string[] = [];
  selectedResourceTypeFilter: string = 'Any';
  priorityFilterOptions: string[] = [ "Normal", "High", "Critical" ];
  selectedPriorityFilter: string = 'Any';
  queryPhaseFilterOptions: string[] = [ "Initial", "Supplemental", "Referential", "Polling", "Monitoring" ];
  selectedQueryPhaseFilter: string = 'Any';
  queryTypeFilterOptions: string[] = [ "Read", "Search", "SearchPost", "BulkDataRequest", "BulkDataPoll" ];
  selectedQueryTypeFilter: string = 'Any';
  statusFilterOptions: string[] = [ "Pending", "Ready", "Processing", "Completed", "Failed", "Cancelled", "MaxRetriesReached", "ConfigurationMissing", "Skipped", "Queued"];
  selectedStatusFilter: string[] = [];
  targetPageNumber: number | null = null;
  selectedLogIds: Set<string> = new Set<string>();
  isAllSelected: boolean = false;
  includeDeleted: boolean = false;
  isStuckLogsFilterActive: boolean = false;
  createdBeforeFilter: string | null = null;
  statusChartExpanded: boolean = false;
  statusChartData: Record<string, number> = {};
  statusChartColors: Record<string, string> = {
    'Pending': '#bdbdbd',
    'Ready': '#ff9800',
    'Queued': '#0ea5e9',
    'Processing': '#1f77b4',
    'Completed': '#2ca02c',
    'Failed': '#d62728',
    'MaxRetriesReached': '#a10303ff',
    'ConfigurationMissing': '#b45309',
    'Skipped': '#9e9e9e',
    'Cancelled': '#6b7280'
  };
  statusChartLoading = false;
  statusChartReportId = '';
  statusChartPatientId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private loadingService: LoadingService,
    private tenantService: TenantService,
    private reportService: ReportService,
    private acquisitionLogService: AcquisitionLogService,
    private snackBar: MatSnackBar) { }

  private readonly TERMINAL_STATUSES = ['Completed', 'MaxRetriesReached', 'ConfigurationMissing', 'Skipped', 'Cancelled'];
  cancelMinAgeHours: number = 0;
  isReportSubmitted: boolean = false;
  submittedDate?: Date;

  private get cancelMinAgeMs(): number {
    return this.cancelMinAgeHours * 60 * 60 * 1000;
  }

  private readonly EXECUTE_INELIGIBLE_STATUSES = ['Completed', 'Skipped'];

  isLogExecuteEligible(log: AcquisitionLogSummary): boolean {
    if (this.isReportSubmitted) return false;
    return log.status !== 'Completed' && !this.EXECUTE_INELIGIBLE_STATUSES.includes(log.status);
  }

  isLogCancelEligible(log: AcquisitionLogSummary): boolean {
    if (this.isReportSubmitted) return false;
    if (log.status === 'Completed') return false;
    if (this.TERMINAL_STATUSES.includes(log.status)) return false;
    if (this.cancelMinAgeHours === 0) return true;
    if (!log.createDate) return false;
    // Match backend: CreateDate <= UtcNow.AddHours(-minAgeHours)
    const minAgeCutoff = Date.now() - this.cancelMinAgeMs;
    return new Date(log.createDate).getTime() <= minAgeCutoff;
  }

  get hasActionEligibleOnPage(): boolean {
    if (this.isReportSubmitted) return false;
    return this.acquisitionLogs.some(l => !this.TERMINAL_STATUSES.includes(l.status));
  }

  get executeEligibleSelectedCount(): number {
    if (this.isAllSelected) {
      return this.acquisitionLogs.filter(l => this.isLogExecuteEligible(l)).length;
    }
    return this.acquisitionLogs.filter(l => this.selectedLogIds.has(l.id) && this.isLogExecuteEligible(l)).length;
  }

  get canBulkExecute(): boolean {
    if (this.isReportSubmitted) return false;
    if (this.isAllSelected) {
      return true;
    }
    return this.executeEligibleSelectedCount > 0;
  }

  get eligibleSelectedCount(): number {
    if (this.isAllSelected) {
      return this.acquisitionLogs.filter(l => this.isLogCancelEligible(l)).length;
    }
    return this.acquisitionLogs.filter(l => this.selectedLogIds.has(l.id) && this.isLogCancelEligible(l)).length;
  }

  get canBulkCancel(): boolean {
    if (this.isReportSubmitted) return false;
    if (this.isAllSelected) {
      // In Select-All mode the count shown is paginationMetadata.totalCount, which only
      // matches cancel-eligibility when the Cancellable Logs filter is active.
      return this.isStuckLogsFilterActive;
    }
    return this.eligibleSelectedCount > 0;
  }

  get hasCancelEligibleOnPage(): boolean {
    return this.acquisitionLogs.some(l => this.isLogCancelEligible(l));
  }

  selectCancelEligibleOnPage(): void {
    this.isAllSelected = false;
    this.selectedLogIds.clear();
    this.acquisitionLogs
      .filter(l => this.isLogCancelEligible(l))
      .forEach(l => this.selectedLogIds.add(l.id));
  }

  ngOnInit(): void {
    const savedPageSize = localStorage.getItem(this.PAGE_SIZE_KEY);
    if (savedPageSize) {
      this.defaultPageSize = +savedPageSize;
    }

    this.paginationMetadata.pageNumber = this.defaultPageNumber;
    this.paginationMetadata.pageSize = this.defaultPageSize;
    this.targetPageNumber = this.defaultPageNumber + 1;

    this.loadingService.show();

    this.route.queryParamMap.subscribe(params => {
      const reportId = params.get('reportId');
      const facilityId = params.get('facilityId');
      const patientId = params.get('patientId');
      if (reportId) {
        this.reportIdFilter = reportId;
        this.reportIdFromRoute = reportId;
        this.reportFacilityId = facilityId ?? '';
        this.loadReportInfo(reportId);
      } else {
        this.reportIdFilter = '';
        this.reportIdFromRoute = '';
        this.reportFacilityId = '';
        this.isReportSubmitted = false;
        this.submittedDate = undefined;
      }

      if (patientId) {
        this.patientFilter = patientId;
        this.patientIdFromRoute = patientId;
      } else {
        this.patientFilter = '';
        this.patientIdFromRoute = '';
      }
      this.onFilterApplication();
    });

    const storedExpandedState = localStorage.getItem('acquisitionLogStatusChartExpanded');
    this.statusChartExpanded = storedExpandedState ? JSON.parse(storedExpandedState) : false;

    forkJoin([
      this.tenantService.getAllFacilities(),
      this.acquisitionLogService.getAcquisitionLogs(this.patientFilter === '' ? null : this.patientFilter, this.selectedFacilityFilter === 'Any' ? null : this.selectedFacilityFilter, this.reportIdFilter === '' ? null : this.reportIdFilter, null, null, null, null, null, null, null, null, this.defaultPageNumber, this.defaultPageSize, false, this.includeDeleted)

        ]).subscribe({
          next: (response) => {
            this.facilityFilterOptions = response[0];
            this.resourceTypeFilterOptions = this.resourceTypes;
            this.filteredResourceTypeOptions = [...this.resourceTypes];
            this.acquisitionLogs = response[1].records;
            this.paginationMetadata = response[1].metadata;
            this.syncTargetPageNumber();
            this.loadStatusCounts();

            this.loadingService.hide();
          },
          error: (error) => {
            console.error('Error loading audit logs:', error);
            this.loadingService.hide();
          }
        });
  }

  loadLogs(pageNumber: number, pageSize: number, showLoadingIndicator: boolean): void {

    this.acquisitionLogService.getAcquisitionLogs(
      this.patientFilter !== 'Any' ? this.patientFilter : null,
      this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null,
      this.reportIdFilter.length > 0 ? this.reportIdFilter : null,
      this.selectedResourceTypeFilter !== 'Any' ? this.selectedResourceTypeFilter : null,
      this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null,
      this.selectedQueryTypeFilter !== 'Any' ? this.selectedQueryTypeFilter : null,
      this.selectedQueryPhaseFilter !== 'Any' ? this.selectedQueryPhaseFilter : null,
      this.selectedStatusFilter.length > 0 ? this.selectedStatusFilter : null,
      this.selectedPriorityFilter !== 'Any' ? this.selectedPriorityFilter : null,
      this.sortBy,
      this.sortOrder,
      pageNumber,
      pageSize,
      showLoadingIndicator,
      this.includeDeleted,
      this.createdBeforeFilter
    )
    .pipe(
      finalize(() => this.loadingService.hide())
    )
    .subscribe({
      next: (response) => {
        this.acquisitionLogs = response.records;
        this.paginationMetadata = response.metadata;
        this.syncTargetPageNumber();
      },
      error: (error) => {
        console.error('Error loading acquisition logs:', error);
      }
    });
  }

  pagedEvent(event: PageEvent) {
    this.targetPageNumber = event.pageIndex + 1;
    localStorage.setItem(this.PAGE_SIZE_KEY, event.pageSize.toString());
    this.loadLogs(event.pageIndex, event.pageSize, true);
    this.clearSelection();
  }

  filterByPatient(patientId: string) {
    this.patientFilter = patientId;
    this.applyFilters();
  }

  filterByResourceType(resourceType: string) {
    this.selectedResourceTypeFilter = resourceType;
    this.applyFilters();
  }

  onResourceTypeInput(event: any) {
    const value = event.target.value;
    this._filterResourceTypes(value);
  }

  onResourceTypeFocus(event: any) {
    event.target.select();
  }

  onResourceTypeBlur() {
    // Small delay to allow mat-autocomplete selection to process
    setTimeout(() => {
      if (this.selectedResourceTypeFilter !== 'Any' && !this.resourceTypes.includes(this.selectedResourceTypeFilter)) {
        this.selectedResourceTypeFilter = 'Any';
        this._filterResourceTypes('');
      }
    }, 200);
  }

  private _filterResourceTypes(value: string) {
    const filterValue = (value || '').toLowerCase();
    this.filteredResourceTypeOptions = this.resourceTypeFilterOptions.filter(option =>
      option.toLowerCase().includes(filterValue)
    );
  }

  applyFilters(): void {
    this.loadLogs(this.defaultPageNumber, this.getCurrentPageSize(), true);
    this.loadStatusCounts();
    this.onFilterApplication();
    this.clearSelection();
  }

  clearFilter(filterName: string): void {
    switch (filterName) {
      case 'patient':
        this.patientFilter = this.patientIdFromRoute;
        break;
      case 'facility':
        this.selectedFacilityFilter = 'Any';
        break;
      case 'priority':
        this.selectedPriorityFilter = 'Any';
        break;
      case 'status':
        this.selectedStatusFilter = [];
        this.isStuckLogsFilterActive = false;
        this.createdBeforeFilter = null;
        break;
      case 'reportId':
        this.reportIdFilter = this.reportIdFromRoute;
        break;
      case 'resourceId':
        this.resourceIdFilter = '';
        break;
      case 'resourceType':
        this.selectedResourceTypeFilter = 'Any';
        this._filterResourceTypes('');
        break;
      case 'queryType':
        this.selectedQueryTypeFilter = 'Any';
        break;
      case 'queryPhase':
        this.selectedQueryPhaseFilter = 'Any';
        break;
    }
    this.applyFilters();
  }

  isFilterActive(filterName: string): boolean {
    switch (filterName) {
      case 'patient':
        return this.patientFilter !== this.patientIdFromRoute;
      case 'facility':
        return this.selectedFacilityFilter !== 'Any';
      case 'priority':
        return this.selectedPriorityFilter !== 'Any';
      case 'status':
        return this.selectedStatusFilter.length > 0;
      case 'reportId':
        return this.reportIdFilter !== this.reportIdFromRoute;
      case 'resourceId':
        return this.resourceIdFilter !== '';
      case 'resourceType':
        return this.selectedResourceTypeFilter !== 'Any';
      case 'queryType':
        return this.selectedQueryTypeFilter !== 'Any';
      case 'queryPhase':
        return this.selectedQueryPhaseFilter !== 'Any';
      default:
        return false;
    }
  }

  onFilterApplication(): void {
    this.allowLogSelection = (
      this.reportIdFilter !== '' ||
      this.patientFilter !== '' ||
      this.selectedFacilityFilter !== 'Any' ||
      this.selectedResourceTypeFilter !== 'Any' ||
      this.resourceIdFilter !== '' ||
      this.selectedQueryTypeFilter !== 'Any' ||
      this.selectedQueryPhaseFilter !== 'Any' ||
      this.selectedStatusFilter.length > 0 ||
      this.selectedPriorityFilter !== 'Any'
    );
  }

  onIncludeDeletedChange(): void {
    this.tenantService.getAllFacilities(this.includeDeleted).subscribe({
      next: (facilities) => {
        this.facilityFilterOptions = facilities;
        if (this.selectedFacilityFilter !== 'Any' && !facilities[this.selectedFacilityFilter]) {
          this.selectedFacilityFilter = 'Any';
        }
        this.applyFilters();
      },
      error: (error) => console.error('Error loading facilities:', error)
    });
  }

  refreshLogs(): void {
    const pageIndex = this.paginationMetadata?.pageNumber ?? this.defaultPageNumber;
    const pageSize = this.paginationMetadata?.pageSize ?? this.defaultPageSize;
    this.loadLogs(pageIndex, pageSize, true);
    this.loadStatusCounts();
  }

  clearFilters(): void {
    this.patientFilter = this.patientIdFromRoute;
    this.resourceIdFilter = '';
    this.selectedFacilityFilter = 'Any';
    this.reportIdFilter = this.reportIdFromRoute;
    this.selectedResourceTypeFilter = 'Any';
    this.filteredResourceTypeOptions = this.resourceTypeFilterOptions;
    this.selectedPriorityFilter = 'Any';
    this.selectedQueryPhaseFilter = 'Any';
    this.selectedQueryTypeFilter = 'Any';
    this.selectedStatusFilter = [];
    this.isStuckLogsFilterActive = false;
    this.createdBeforeFilter = null;
    this.onFilterApplication();
    this.clearSelection();
    this.loadLogs(this.defaultPageNumber, this.defaultPageSize, true);
    this.loadStatusCounts();
  }

  toggleSelection(logId: string) {
    if (this.isAllSelected) {
      this.acquisitionLogs.forEach(log => this.selectedLogIds.add(log.id));
      this.isAllSelected = false;
    }

    if (this.selectedLogIds.has(logId)) {
      this.selectedLogIds.delete(logId);
    } else {
      this.selectedLogIds.add(logId);
    }
  }

  isLogSelected(logId: string): boolean {
    return this.isAllSelected || this.selectedLogIds.has(logId);
  }

  selectAll() {
    this.isAllSelected = true;
    this.selectedLogIds.clear();
  }

  clearSelection() {
    this.isAllSelected = false;
    this.selectedLogIds.clear();
  }

  applyStuckLogsFilter(): void {
    this.selectedStatusFilter = ['Pending', 'Ready', 'Queued', 'Processing', 'Failed'];
    this.isStuckLogsFilterActive = true;
    this.createdBeforeFilter = this.cancelMinAgeHours > 0
      ? new Date(Date.now() - this.cancelMinAgeMs).toISOString()
      : null;
    this.applyFilters();
  }

  onCancelMinAgeChange(): void {
    if (this.cancelMinAgeHours == null || isNaN(this.cancelMinAgeHours) || this.cancelMinAgeHours < 0) {
      this.cancelMinAgeHours = 0;
    }
    this.cancelMinAgeHours = Math.floor(this.cancelMinAgeHours);
    if (this.isStuckLogsFilterActive) {
      this.applyStuckLogsFilter();
    }
  }

  toggleCancellableFilter(checked: boolean): void {
    if (checked) {
      this.applyStuckLogsFilter();
    } else {
      this.selectedStatusFilter = [];
      this.isStuckLogsFilterActive = false;
      this.createdBeforeFilter = null;
      this.applyFilters();
    }
  }

  handleLogCancelled(logId: string): void {
    const index = this.acquisitionLogs.findIndex(log => log.id === logId);
    if (index !== -1) {
      this.acquisitionLogs[index] = { ...this.acquisitionLogs[index], status: 'Cancelled' };
    }
    setTimeout(() => this.refreshLogs(), 1500);
  }

  bulkExecute() {
    const BULK_EXECUTE_MAX = 1000;

    if (!this.isAllSelected && this.selectedLogIds.size === 0) {
      return;
    }

    if (!this.isAllSelected) {
      const eligibleIds = this.acquisitionLogs
        .filter(l => this.selectedLogIds.has(l.id) && this.isLogExecuteEligible(l))
        .map(l => l.id);

      this.selectedLogIds.clear();
      eligibleIds.forEach(id => this.selectedLogIds.add(id));

      if (this.selectedLogIds.size === 0) {
        return;
      }

      if (this.selectedLogIds.size > BULK_EXECUTE_MAX) {
        alert(`Cannot execute more than ${BULK_EXECUTE_MAX} acquisition logs at once. You selected ${this.selectedLogIds.size}.`);
        return;
      }
    }

    const confirmMsg = this.isAllSelected
      ? `Execute all acquisition logs matching the current filters?`
      : `Execute ${this.selectedLogIds.size} acquisition log${this.selectedLogIds.size === 1 ? '' : 's'}?`;

    if (!window.confirm(confirmMsg)) {
      return;
    }

    if (this.isAllSelected) {
      this.selectedLogIds.clear();
    }

    this.loadingService.show();
    let obs$;
    if (this.isAllSelected) {
      obs$ = this.acquisitionLogService.bulkExecuteAcquisitionLogsByFilter(
        this.patientFilter !== 'Any' ? this.patientFilter : null,
        this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null,
        this.reportIdFilter.length > 0 ? this.reportIdFilter : null,
        this.selectedResourceTypeFilter !== 'Any' ? this.selectedResourceTypeFilter : null,
        this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null,
        this.selectedQueryTypeFilter !== 'Any' ? this.selectedQueryTypeFilter : null,
        this.selectedQueryPhaseFilter !== 'Any' ? this.selectedQueryPhaseFilter : null,
        this.selectedStatusFilter.length > 0 ? this.selectedStatusFilter : null,
        this.selectedPriorityFilter !== 'Any' ? this.selectedPriorityFilter : null,
        this.createdBeforeFilter
      );
    } else {
      obs$ = this.acquisitionLogService.bulkExecuteAcquisitionLogs(Array.from(this.selectedLogIds));
    }

    obs$.pipe(
      finalize(() => {
        this.loadingService.hide();
        this.clearSelection();
        this.refreshLogs();
      })
    ).subscribe({
      next: () => {
        SnackbarHelper.showSuccessMessage(this.snackBar, 'Acquisition logs queued for execution.');
      },
      error: (error) => {
        console.error('Error triggering bulk execution:', error);
        SnackbarHelper.showErrorMessage(this.snackBar, 'Error queueing acquisition logs for execution.');
      }
    });
  }

  bulkCancel() {
    if (!this.isAllSelected && this.selectedLogIds.size === 0) {
      return;
    }

    // Capture select-all state before clearSelection() resets it
    const wasAllSelected = this.isAllSelected;

    // Filter to only cancel-eligible ids (non-terminal + age check). Ineligible selections are dropped.
    const eligibleIds = wasAllSelected
      ? []
      : this.acquisitionLogs
          .filter(l => this.selectedLogIds.has(l.id) && this.isLogCancelEligible(l))
          .map(l => l.id);

    if (!wasAllSelected && eligibleIds.length === 0) {
      SnackbarHelper.showErrorMessage(this.snackBar, 'None of the selected logs are eligible for cancellation.');
      return;
    }

    const skipped = wasAllSelected ? 0 : this.selectedLogIds.size - eligibleIds.length;
    const confirmMsg = wasAllSelected
      ? `Cancel all acquisition logs matching the current filters? This cannot be undone.`
      : `Cancel ${eligibleIds.length} acquisition log${eligibleIds.length === 1 ? '' : 's'}?${skipped > 0 ? ` (${skipped} ineligible will be skipped.)` : ''} This cannot be undone.`;

    if (!window.confirm(confirmMsg)) {
      return;
    }

    this.loadingService.show();
    let obs$;
    if (wasAllSelected) {
      obs$ = this.acquisitionLogService.cancelAcquisitionLogsByFilter(
        this.patientFilter !== 'Any' ? this.patientFilter : null,
        this.selectedFacilityFilter !== 'Any' ? this.selectedFacilityFilter : null,
        this.reportIdFilter.length > 0 ? this.reportIdFilter : null,
        this.selectedResourceTypeFilter !== 'Any' ? this.selectedResourceTypeFilter : null,
        this.resourceIdFilter.length > 0 ? this.resourceIdFilter : null,
        this.selectedQueryTypeFilter !== 'Any' ? this.selectedQueryTypeFilter : null,
        this.selectedQueryPhaseFilter !== 'Any' ? this.selectedQueryPhaseFilter : null,
        this.selectedStatusFilter.length > 0 ? this.selectedStatusFilter : null,
        this.selectedPriorityFilter !== 'Any' ? this.selectedPriorityFilter : null,
        this.createdBeforeFilter,
        this.cancelMinAgeHours
      );
    } else {
      obs$ = this.acquisitionLogService.cancelBulkAcquisitionLogs(eligibleIds, this.cancelMinAgeHours);
    }

    obs$.pipe(
      finalize(() => {
        this.loadingService.hide();
        this.clearSelection();
      })
    ).subscribe({
      next: (result) => {
        const cancelled = result?.cancelled ?? 0;
        const requested = result?.requested ?? 0;
        const ineligible = result?.ineligible ?? 0;

        // Update status in-place for immediate visual feedback
        if (cancelled > 0) {
          if (wasAllSelected) {
            this.acquisitionLogs.forEach((log, i) => {
              if (this.isLogCancelEligible(log)) {
                this.acquisitionLogs[i] = { ...log, status: 'Cancelled' };
              }
            });
          } else {
            const eligibleSet = new Set(eligibleIds);
            this.acquisitionLogs.forEach((log, i) => {
              if (eligibleSet.has(log.id)) {
                this.acquisitionLogs[i] = { ...log, status: 'Cancelled' };
              }
            });
          }
        }

        if (cancelled === 0) {
          SnackbarHelper.showErrorMessage(this.snackBar, `No logs were cancelled. ${ineligible} were not eligible (terminal status or less than ${this.cancelMinAgeHours} hours old).`);
        } else if (ineligible > 0) {
          SnackbarHelper.showSuccessMessage(this.snackBar, `Cancelled ${cancelled} log${cancelled === 1 ? '' : 's'} (${ineligible} were not eligible).`);
        } else {
          SnackbarHelper.showSuccessMessage(this.snackBar, `Cancelled ${cancelled} log${cancelled === 1 ? '' : 's'}.`);
        }

        // Refresh after delay so the user sees the status change before filters take effect
        setTimeout(() => this.refreshLogs(), 1500);
      },
      error: (error) => {
        console.error('Error triggering bulk cancellation:', error);
        SnackbarHelper.showErrorMessage(this.snackBar, 'Error cancelling acquisition logs.');
      }
    });
  }

  disableLogsByFacility(): void {
    const facilityId = this.selectedFacilityFilter;
    if (!facilityId || facilityId === 'Any') return;

    this.loadingService.show();
    this.acquisitionLogService.softDeleteByFacility(facilityId)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (count) => {
          console.log(`Disabled ${count} acquisition log(s) for facility ${facilityId}`);
          this.refreshLogs();
        },
        error: (error) => {
          console.error('Error disabling acquisition logs:', error);
        }
      });
  }

  restoreLogsByFacility(): void {
    const facilityId = this.selectedFacilityFilter;
    if (!facilityId || facilityId === 'Any') return;

    this.loadingService.show();
    this.acquisitionLogService.restoreByFacility(facilityId)
      .pipe(finalize(() => this.loadingService.hide()))
      .subscribe({
        next: (count) => {
          console.log(`Restored ${count} acquisition log(s) for facility ${facilityId}`);
          this.refreshLogs();
        },
        error: (error) => {
          console.error('Error restoring acquisition logs:', error);
        }
      });
  }

  onSort(column: string): void {
    if (this.sortBy !== column) {
      this.sortBy = column;
      this.sortOrder = 'ascending';
    } else if (this.sortOrder === 'ascending') {
      this.sortOrder = 'descending';
    } else if (this.sortOrder === 'descending') {
      this.sortBy = null;
      this.sortOrder = null;
    } else {
      this.sortOrder = 'ascending';
    }

    this.loadLogs(this.defaultPageNumber, this.getCurrentPageSize(), true);
    this.clearSelection();
  }

  getSortIcon(column: string) {
    if (this.sortBy !== column) return this.faSort;
    if (this.sortOrder === 'ascending') return this.faSortUp;
    if (this.sortOrder === 'descending') return this.faSortDown;

    return this.faSort;
  }

  handleLogScheduled(queryLogId: string) {
    let scheduledLogIndex = this.acquisitionLogs.findIndex((log) => log.id === queryLogId);
    this.acquisitionLogs[scheduledLogIndex].status = 'Ready';
  }

  returnToReport(): void {
    if (!this.reportIdFromRoute || !this.reportFacilityId) {
      return;
    }

    this.router.navigate(['/tenant/facility', this.reportFacilityId, 'report', this.reportIdFromRoute]);
  }

  goToPage(): void {
    const totalPages = this.getTotalPages();
    const requestedPage = Number(this.targetPageNumber);
    if (!Number.isFinite(requestedPage)) {
      this.syncTargetPageNumber();
      return;
    }

    const normalizedPage = Math.floor(requestedPage);
    if (normalizedPage < 1 || (totalPages && normalizedPage > totalPages)) {
      this.syncTargetPageNumber();
      return;
    }

    const targetIndex = normalizedPage - 1;
    if (targetIndex === this.getCurrentPageNumber()) {
      return;
    }

    this.loadLogs(targetIndex, this.getCurrentPageSize(), true);
  }

  canNavigateToPage(): boolean {
    const totalPages = this.getTotalPages();
    const requestedPage = Number(this.targetPageNumber);
    if (!Number.isFinite(requestedPage)) {
      return false;
    }

    const normalizedPage = Math.floor(requestedPage);
    if (normalizedPage < 1) {
      return false;
    }

    if (totalPages && normalizedPage > totalPages) {
      return false;
    }

    return normalizedPage - 1 !== this.getCurrentPageNumber();
  }

  private getCurrentPageNumber(): number {
    return this.paginationMetadata.pageNumber ?? this.defaultPageNumber;
  }

  private getCurrentPageSize(): number {
    return this.paginationMetadata.pageSize ?? this.defaultPageSize;
  }

  private getTotalPages(): number {
    if (this.paginationMetadata.totalPages) {
      return this.paginationMetadata.totalPages;
    }

    const totalCount = this.paginationMetadata.totalCount ?? 0;
    const pageSize = this.getCurrentPageSize();
    if (!pageSize) {
      return 0;
    }

    return Math.ceil(totalCount / pageSize);
  }

  private syncTargetPageNumber(): void {
    this.targetPageNumber = this.getCurrentPageNumber() + 1;
  }

  private loadReportInfo(reportId: string): void {
    this.reportService.getReportSchedule(reportId).subscribe({
      next: (report) => {
        this.reportFacilityId = report.facilityId;
        this.isReportSubmitted = !!report.submitReportDateTime;
        this.submittedDate = report.submitReportDateTime;
      },
      error: (error) => {
        console.error('Error loading report schedule:', error);
      }
    });
  }

  get showStatusChart(): boolean {
    return this.reportIdFilter.trim().length > 0;
  }

  get hasStatusChartData(): boolean {
    return Object.keys(this.statusChartData).length > 0;
  }

  private loadStatusCounts(): void {
    const reportId = this.reportIdFilter.trim();
    if (!reportId) {
      this.clearStatusCounts();
      return;
    }

    const patientId = this.patientFilter.trim();
    this.statusChartLoading = true;
    this.statusChartReportId = reportId;
    this.statusChartPatientId = patientId;

    this.acquisitionLogService.getAcquisitionLogStatusStatistics(reportId, patientId.length > 0 ? patientId : null)
      .pipe(
        finalize(() => {
          this.statusChartLoading = false;
        })
      )
      .subscribe({
        next: (response: IDataAcquisitionLogStatusStatistics) => {
          this.statusChartReportId = response.reportId;
          this.statusChartPatientId = response.patientId ?? '';
          this.statusChartData = this.toStatusChartData(response.statuses);
        },
        error: (error) => {
          console.error('Error loading acquisition log status counts:', error);
          this.clearStatusCounts();
        }
      });
  }

  private toStatusChartData(statuses: IDataAcquisitionLogStatusCount[]): Record<string, number> {
    return statuses.reduce((acc, status) => {
      acc[status.name] = status.count;
      return acc;
    }, {} as Record<string, number>);
  }

  private clearStatusCounts(): void {
    this.statusChartData = {};
    this.statusChartLoading = false;
    this.statusChartReportId = '';
    this.statusChartPatientId = '';
  }

  onStatusChartToggle(expanded: boolean): void {
    this.statusChartExpanded = expanded;
    localStorage.setItem('acquisitionLogStatusChartExpanded', JSON.stringify(this.statusChartExpanded));
  }

}
