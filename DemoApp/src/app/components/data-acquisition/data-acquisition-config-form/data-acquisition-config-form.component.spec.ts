import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionConfigFormComponent } from './data-acquisition-config-form.component';

describe('DataAcquisitionConfigFormComponent', () => {
  let component: DataAcquisitionConfigFormComponent;
  let fixture: ComponentFixture<DataAcquisitionConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ DataAcquisitionConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
