import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { FacilityConfigFormComponent } from './facility-config-form.component';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { MeasureDefinitionService } from 'src/app/services/gateway/measure-definition/measure.service';
import { AppConfigService } from 'src/app/services/app-config.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IFacilityConfigModel } from 'src/app/interfaces/tenant/facility-config-model.interface';
import { IVendorVersion } from 'src/app/interfaces/tenant/vendor-interface';
import { FormMode } from 'src/app/models/FormMode.enum';

describe('FacilityConfigFormComponent', () => {
  let component: FacilityConfigFormComponent;
  let fixture: ComponentFixture<FacilityConfigFormComponent>;

  const vendors: IVendorVersion[] = [
    { id: 'epic-version-id', vendorId: 'epic-id', vendorName: 'Epic', version: '2026.1' },
    { id: 'cerner-version-id', vendorId: 'cerner-id', vendorName: 'Cerner', version: '2026.1' }
  ];
  const facility: IFacilityConfigModel = {
    facilityId: 'facility-id',
    facilityName: 'Facility',
    timeZone: 'UTC',
    vendor: { id: 'epic-id', name: 'Epic' },
    vendorVersionId: 'epic-version-id',
    scheduledReports: { daily: [], monthly: [], weekly: [] }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FacilityConfigFormComponent, NoopAnimationsModule],
      providers: [
        { provide: TenantService, useValue: { getVendorVersions: () => of(vendors) } },
        { provide: MeasureDefinitionService, useValue: { getMeasureDefinitionConfigurations: () => of([]) } },
        { provide: AppConfigService, useValue: { loadConfig: () => Promise.resolve({ allowAlphaNumericFacilityId: true, dmrpEnabled: false }) } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FacilityConfigFormComponent);
    component = fixture.componentInstance;
    component.item = facility;
    fixture.detectChanges(false);
    await fixture.whenStable();
    fixture.detectChanges(false);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('loads API vendor versions and matches the existing facility vendor by ID', () => {
    expect(component.vendors).toEqual(vendors);
    expect(component.vendorControl.value).toEqual(vendors[0]);
    expect(component.compareVendors(component.vendorControl.value, vendors[0])).toBeTrue();
  });
});

/**
 * With DMRP enabled the Tenant API derives a facility's schedule from its reporting plans and refuses
 * a request that carries one. Editing is the case that can quietly break: the form loads the stored
 * schedule into the report controls, so submitting an otherwise untouched facility would send it back
 * and be rejected. The arrays have to be emptied on the way out, not merely left unselected.
 */
describe('FacilityConfigFormComponent with DMRP enabled', () => {
  let component: FacilityConfigFormComponent;
  let fixture: ComponentFixture<FacilityConfigFormComponent>;
  let submitted: { daily: string[]; monthly: string[]; weekly: string[] } | undefined;

  const scheduledFacility: IFacilityConfigModel = {
    id: 'facility-guid',
    facilityId: 'facility-id',
    facilityName: 'Facility',
    timeZone: 'UTC',
    vendor: { id: 'epic-id', name: 'Epic' },
    vendorVersionId: 'epic-version-id',
    scheduledReports: { daily: ['daily-measure'], monthly: ['monthly-measure'], weekly: [] }
  };

  beforeEach(async () => {
    submitted = undefined;

    await TestBed.configureTestingModule({
      imports: [FacilityConfigFormComponent, NoopAnimationsModule],
      providers: [
        {
          provide: TenantService,
          useValue: {
            getVendorVersions: () => of([]),
            createFacility: (_id: string, _name: string, _tz: string, scheduledReports: any) => {
              submitted = scheduledReports;
              return of({ id: 'new-facility' });
            },
            updateFacility: (_key: string, _id: string, _name: string, _tz: string, scheduledReports: any) => {
              submitted = scheduledReports;
              return of({ id: 'facility-guid' });
            }
          }
        },
        { provide: MeasureDefinitionService, useValue: { getMeasureDefinitionConfigurations: () => of([]) } },
        {
          provide: AppConfigService,
          useValue: { loadConfig: () => Promise.resolve({ allowAlphaNumericFacilityId: true, dmrpEnabled: true }) }
        },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FacilityConfigFormComponent);
    component = fixture.componentInstance;
    component.item = scheduledFacility;
    fixture.detectChanges(false);
    await fixture.whenStable();
    fixture.detectChanges(false);
  });

  it('reads the flag from the app configuration', () => {
    expect(component.dmrpEnabled).toBeTrue();
  });

  it('submits an empty schedule on edit even though the controls hold the stored one', () => {
    // The form loaded the facility, so the controls are populated.
    expect(component.monthlyReportsControl.value).toEqual(['monthly-measure']);

    component.formMode = FormMode.Edit;
    component.submitConfiguration();

    expect(submitted).toEqual({ daily: [], monthly: [], weekly: [] });
  });

  it('submits an empty schedule when no reports are selected', () => {
    component.monthlyReportsControl.setValue([]);
    component.dailyReportsControl.setValue([]);
    component.weeklyReportsControl.setValue([]);
    component.facilityConfigForm.updateValueAndValidity();

    component.formMode = FormMode.Create;
    component.submitConfiguration();

    expect(submitted).toEqual({ daily: [], monthly: [], weekly: [] });
  });
});
