import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DataAcquisitionReqeustedFormComponent } from './data-acquisition-reqeusted-form.component';

describe('DataAcquisitionReqeustedFormComponent', () => {
  let component: DataAcquisitionReqeustedFormComponent;
  let fixture: ComponentFixture<DataAcquisitionReqeustedFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ DataAcquisitionReqeustedFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DataAcquisitionReqeustedFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
