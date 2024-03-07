import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MeasureDefViewComponent } from './measure-def-view.component';

describe('FacilityViewComponent', () => {
  let component: MeasureDefViewComponent;
  let fixture: ComponentFixture<MeasureDefViewComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MeasureDefViewComponent]
    });
    fixture = TestBed.createComponent(MeasureDefViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
