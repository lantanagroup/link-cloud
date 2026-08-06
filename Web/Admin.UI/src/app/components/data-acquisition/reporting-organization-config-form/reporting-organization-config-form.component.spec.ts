import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';

import { ReportingOrganizationConfigFormComponent } from './reporting-organization-config-form.component';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import { FormMode } from 'src/app/models/FormMode.enum';

describe('ReportingOrganizationConfigFormComponent', () => {
  let component: ReportingOrganizationConfigFormComponent;
  let fixture: ComponentFixture<ReportingOrganizationConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportingOrganizationConfigFormComponent, NoopAnimationsModule],
      providers: [
        {
          provide: DataAcquisitionService,
          useValue: {
            getQueryPlanConfiguration: () => of(null),
            validateFhirPath: () => of({ errors: [], warnings: [] })
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReportingOrganizationConfigFormComponent);
    component = fixture.componentInstance;
    component.formMode = FormMode.Create;
    component.vendor = { id: 'other-vendor-id', name: 'Other EHR' };
    fixture.detectChanges(false);
  });

  it('uses Custom FHIRPath for vendors without a specialized builder', () => {
    expect(component.isEpic).toBeFalse();
    expect(component.isCerner).toBeFalse();
    expect(component.setupMethodControl.value).toBe('manual');
  });
});