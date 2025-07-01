import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VdButtonComponent } from './vd-button.component';

describe('VdButtonComponent', () => {
  let component: VdButtonComponent;
  let fixture: ComponentFixture<VdButtonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VdButtonComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VdButtonComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
