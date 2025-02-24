import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FacilityEditComponent } from './facility-edit.component';

describe('FacilityViewComponent', () => {
  let component: FacilityEditComponent;
  let fixture: ComponentFixture<FacilityEditComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FacilityEditComponent]
    });
    fixture = TestBed.createComponent(FacilityEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
