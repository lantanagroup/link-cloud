import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PatientListacquiredFormComponent } from './patient-listacquired-form.component';

describe('PatientListacquiredFormComponent', () => {
  let component: PatientListacquiredFormComponent;
  let fixture: ComponentFixture<PatientListacquiredFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientListacquiredFormComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PatientListacquiredFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
