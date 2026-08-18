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
        { provide: AppConfigService, useValue: { loadConfig: () => Promise.resolve({ allowAlphaNumericFacilityId: true }) } },
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
