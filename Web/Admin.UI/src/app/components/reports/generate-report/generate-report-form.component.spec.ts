import {ComponentFixture, fakeAsync, flushMicrotasks, TestBed, tick, waitForAsync} from '@angular/core/testing';
import {GenerateReportFormComponent} from './generate-report-form.component';
import {FormBuilder, ReactiveFormsModule} from '@angular/forms';
import {of, throwError} from 'rxjs';
import {MatSnackBar} from '@angular/material/snack-bar';
import {TenantService} from '../../../services/gateway/tenant/tenant.service';
import {MeasureDefinitionService} from '../../../services/gateway/measure-definition/measure.service';
import {Router} from '@angular/router';


import {MatDatepickerModule} from '@angular/material/datepicker';
import {MatNativeDateModule} from '@angular/material/core';


describe('GenerateReportFormComponent', () => {
  let component: GenerateReportFormComponent;
  let fixture: ComponentFixture<GenerateReportFormComponent>;
  let tenantService: jasmine.SpyObj<TenantService>;
  let measureDefinitionService: jasmine.SpyObj<MeasureDefinitionService>;
  let snackBar: jasmine.SpyObj<MatSnackBar>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(waitForAsync(() => {
    // Create spies for services
    tenantService = jasmine.createSpyObj('TenantService', ['autocompleteFacilities', 'getFacilityConfiguration', 'generateAdHocReport', 'checkFacility']);
    measureDefinitionService = jasmine.createSpyObj('MeasureDefinitionService', ['getMeasureDefinitionConfigurations']);
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    router = jasmine.createSpyObj('Router', ['navigate']);

    // Mock service responses
    tenantService.autocompleteFacilities.and.returnValue(of({facility1: 'Facility 1', facility2: 'Facility 2'}));

    tenantService.generateAdHocReport.and.returnValue(of({reportId: 'mockReportId'}));
    tenantService.checkFacility.and.returnValue(of(true));


    tenantService.getFacilityConfiguration.and.returnValue(
      of({
        facilityId: 'facility1',
        facilityName: 'Mock Facility',
        timeZone: 'UTC',
        scheduledReports: {
          daily: ['Daily Report 1', 'Daily Report 2'],
          monthly: ['Monthly Report 1'],
          weekly: ['Weekly Report 1', 'Weekly Report 2']
        }
      })
    );


    measureDefinitionService.getMeasureDefinitionConfigurations.and.returnValue(
      of([{id: 'reportType1'}, {id: 'reportType2'}])
    );

    TestBed.configureTestingModule({
      imports: [ReactiveFormsModule, GenerateReportFormComponent, MatDatepickerModule,  // Import for the datepicker
        MatNativeDateModule   // Import for the Native adapter
      ],
      declarations: [],
      providers: [
        FormBuilder,
        {provide: TenantService, useValue: tenantService},
        {provide: MeasureDefinitionService, useValue: measureDefinitionService},
        {provide: MatSnackBar, useValue: snackBar},
        {provide: Router, useValue: router}
      ]
    }).compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(GenerateReportFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  describe('Component Initialization', () => {
    it('should create the component', () => {
      expect(component).toBeTruthy();
    });

    it('should initialize the form with default values and controls', () => {
      expect(component.generateReportForm.contains('facilityId')).toBeTrue();
      expect(component.generateReportForm.contains('reportingCadence')).toBeTrue();
      expect(component.generateReportForm.contains('startDate')).toBeTrue();
      expect(component.generateReportForm.contains('endDate')).toBeTrue();
      expect(component.generateReportForm.contains('reportTypes')).toBeTrue();
      expect(component.generateReportForm.contains('bypassSubmission')).toBeTrue();
    });

    it('should initialize reportTypes from MeasureDefinitionService', fakeAsync(() => {
      tick();
      expect(component.reportTypes).toEqual(['reportType1', 'reportType2']);
    }));

    it('should initialize filteredFacilities as an observable', fakeAsync(() => {
      component.facilityInputControl.setValue('Facility');
      tick(300); // Simulate debounce

      component.filteredFacilities.subscribe(filtered => {
        expect(filtered).toEqual([
          {facilityId: 'facility1', facilityName: 'Facility 1'},
          {facilityId: 'facility2', facilityName: 'Facility 2'}
        ]);
      });
    }));
  });

  describe('Form Validation', () => {
    it('should mark facilityId as required', () => {
      const facilityIdControl = component.facilityIdControl;
      facilityIdControl.setValue('');
      expect(facilityIdControl.valid).toBeFalse();
      expect(facilityIdControl.errors).toEqual({required: true});

      facilityIdControl.setValue('validFacility');
      expect(facilityIdControl.valid).toBeTrue();
    });

    it('should validate startDate and endDate', () => {
      const startDateControl = component.startDateControl;
      const endDateControl = component.endDateControl;

      startDateControl.setValue('');
      expect(startDateControl.valid).toBeFalse();

      endDateControl.setValue('');
      expect(endDateControl.valid).toBeFalse();

      startDateControl.setValue(new Date());
      endDateControl.setValue(new Date());
      expect(startDateControl.valid).toBeTrue();
      expect(endDateControl.valid).toBeTrue();
    });

    it('should sync end date with start date when cadence is Daily', () => {
      component.reportingCadenceControl.setValue('Daily');
      const testDate = new Date(2026, 1, 1);
      component.startDateControl.setValue(testDate);
      expect(component.endDateControl.value).toEqual(testDate);
      expect(component.endDateControl.disabled).toBeTrue();
    });

    it('should set monthly range correctly in chosenMonthHandler', () => {
      component.reportingCadenceControl.setValue('Monthly');
      const normalizedMonth = new Date(2026, 5, 1); // June 2026
      const mockPicker = jasmine.createSpyObj('MatDatepicker', ['close']);
      component.chosenMonthHandler(normalizedMonth, mockPicker);

      const startDate: Date = component.startDateControl.value;
      const endDate: Date = component.endDateControl.value;

      expect(startDate.getFullYear()).toBe(2026);
      expect(startDate.getMonth()).toBe(5);
      expect(startDate.getDate()).toBe(1);

      expect(endDate.getFullYear()).toBe(2026);
      expect(endDate.getMonth()).toBe(5);
      // Last day of June is 30
      expect(endDate.getDate()).toBe(30);

      expect(mockPicker.close).toHaveBeenCalled();
      expect(component.endDateControl.disabled).toBeTrue();
    });

    it('should enable end date when cadence is Custom', () => {
      component.reportingCadenceControl.setValue('Custom');
      expect(component.endDateControl.enabled).toBeTrue();
    });
  });

  describe('Facility Input Handling', () => {
    it('should handle facility input blur and validate facility', fakeAsync(() => {
      component.facilities = [
        {facilityId: 'facility1', facilityName: 'Facility 1'},
        {facilityId: 'facility2', facilityName: 'Facility 2'}
      ];

      tenantService.checkFacility.and.returnValue(of(false));

      component.facilityInputControl.setValue('Invalid Facility');

      component.onFacilityInputBlur();
      tick(200); // Allow time for the blur timeout

      expect(component.facilityIdControl.errors).toEqual({notFound: true});
      expect(component.facilityIdControl.value).toEqual('Invalid Facility');
    }));

    it('should handle facility selection', () => {
      const facility = {facilityId: 'facility1', facilityName: 'Facility 1'};
      component.facilities = [facility]; // Mock the facilities

      component.onFacilitySelected(facility);

      expect(component.facilityIdControl.value).toBe(facility.facilityId); // Expect facilityId
      expect(component.facilityInputControl.value).toBe(facility.facilityId); // Expect facilityId as well (matches the implementation)

    });
  });

  describe('Form Submission', () => {
    beforeEach(() => {
      component.generateReportForm.patchValue({
        facilityId: 'facility1',
        startDate: new Date(2023, 0, 1),
        endDate: new Date(2023, 0, 31),
        reportTypes: ['reportType1'],
        bypassSubmission: false,
        facilityInput: '',
        patients: [],
        selectedForm: 'manualPatients'
      });
    });

    it('should submit the form successfully when valid', async () => {

      const mockReportId = 'mockReportId';

      tenantService.generateAdHocReport.and.returnValue(of({reportId: 'mockReportId'}));

      await component.generateReport();
      // tick();

      expect(component.lastGeneratedReport).toEqual({
        facilityId: 'facility1',
        reportId: mockReportId
      });

      expect(component.formSubmitted).toBeTrue();
      expect(tenantService.generateAdHocReport).toHaveBeenCalledWith(
        'facility1',
        jasmine.objectContaining({
          reportTypes: ['reportType1'],
          bypassSubmission: false
        })
      );
    });

    it('should display an error when submission fails', fakeAsync(() => {

      tenantService.generateAdHocReport.and.returnValue(
        throwError(() => new Error('Failed to generate the report'))
      );

      component.generateReport();
      flushMicrotasks();
      tick();

      //expect(component.errorMessage).toBe('Submission failed');
      expect(component.formSubmitted).toBeTrue();

      expect(tenantService.generateAdHocReport).toHaveBeenCalled();

    }));
  });

  describe('Utility Functions', () => {
    it('should reset the form correctly', () => {
      component.generateReportForm.patchValue({
        facilityId: 'facility1',
        startDate: new Date(),
        endDate: new Date(),
        reportTypes: ['type1']
      });
      (component as any).resetForm();

      expect(component.generateReportForm.get('facilityId')?.value).toBeNull();
      expect(component.generateReportForm.get('startDate')?.value).toBeNull();
      expect(component.generateReportForm.get('endDate')?.value).toBeNull();
      expect(component.generateReportForm.get('reportTypes')?.value).toBeNull();
    });
  });
});
