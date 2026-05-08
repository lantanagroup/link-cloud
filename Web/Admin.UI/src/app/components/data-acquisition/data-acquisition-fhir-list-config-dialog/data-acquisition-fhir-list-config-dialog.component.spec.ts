import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionFhirListConfigDialogComponent } from './data-acquisition-fhir-list-config-dialog.component';

describe('DataAcquisitionFhirListConfigDialogComponent', () => {
  let component: DataAcquisitionFhirListConfigDialogComponent;
  let fixture: ComponentFixture<DataAcquisitionFhirListConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ DataAcquisitionFhirListConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionFhirListConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
