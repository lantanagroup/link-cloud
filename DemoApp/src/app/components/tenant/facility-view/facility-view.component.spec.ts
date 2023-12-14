import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FacilityViewComponent } from './facility-view.component';

describe('FacilityViewComponent', () => {
  let component: FacilityViewComponent;
  let fixture: ComponentFixture<FacilityViewComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FacilityViewComponent]
    });
    fixture = TestBed.createComponent(FacilityViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
