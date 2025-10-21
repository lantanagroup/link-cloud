import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PatientListAcquiredComponent } from './patient-listacquired-form.component';

describe('PatientListAcquiredComponent', () => {
  let component: PatientListAcquiredComponent;
  let fixture: ComponentFixture<PatientListAcquiredComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PatientListAcquiredComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PatientListAcquiredComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
