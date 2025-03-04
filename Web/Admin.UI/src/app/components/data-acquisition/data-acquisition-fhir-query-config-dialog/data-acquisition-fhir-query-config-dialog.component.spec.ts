import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionFhirQueryConfigDialogComponent } from './data-acquisition-fhir-query-config-dialog.component';

describe('DataAcquisitionFhirQueryConfigDialogComponent', () => {
  let component: DataAcquisitionFhirQueryConfigDialogComponent;
  let fixture: ComponentFixture<DataAcquisitionFhirQueryConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionFhirQueryConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionFhirQueryConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
