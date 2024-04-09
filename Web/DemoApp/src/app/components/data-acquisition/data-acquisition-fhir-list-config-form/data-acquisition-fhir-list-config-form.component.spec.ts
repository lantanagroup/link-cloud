import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionFhirListConfigFormComponent } from './data-acquisition-fhir-list-config-form.component';

describe('DataAcquisitionFhirListConfigFormComponent', () => {
  let component: DataAcquisitionFhirListConfigFormComponent;
  let fixture: ComponentFixture<DataAcquisitionFhirListConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionFhirListConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionFhirListConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
