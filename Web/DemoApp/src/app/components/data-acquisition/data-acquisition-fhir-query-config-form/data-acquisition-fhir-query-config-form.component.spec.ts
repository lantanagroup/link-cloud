import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionFhirQueryConfigFormComponent } from './data-acquisition-fhir-query-config-form.component';

describe('DataAcquisitionFhirQueryConfigFormComponent', () => {
  let component: DataAcquisitionFhirQueryConfigFormComponent;
  let fixture: ComponentFixture<DataAcquisitionFhirQueryConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionFhirQueryConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionFhirQueryConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
