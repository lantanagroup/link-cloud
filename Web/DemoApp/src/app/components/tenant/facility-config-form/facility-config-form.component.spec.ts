import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FacilityConfigFormComponent } from './facility-config-form.component';

describe('FacilityConfigFormComponent', () => {
  let component: FacilityConfigFormComponent;
  let fixture: ComponentFixture<FacilityConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ FacilityConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FacilityConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
