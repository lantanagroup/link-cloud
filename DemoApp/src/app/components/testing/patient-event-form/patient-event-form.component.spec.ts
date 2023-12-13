import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PatientEventFormComponent } from './patient-event-form.component';

describe('PatientEventFormComponent', () => {
  let component: PatientEventFormComponent;
  let fixture: ComponentFixture<PatientEventFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ PatientEventFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PatientEventFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
