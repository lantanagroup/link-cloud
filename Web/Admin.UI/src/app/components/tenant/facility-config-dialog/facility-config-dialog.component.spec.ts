import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FacilityConfigDialogComponent } from './facility-config-dialog.component';

describe('FacilityConfigDialogComponent', () => {
  let component: FacilityConfigDialogComponent;
  let fixture: ComponentFixture<FacilityConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ FacilityConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FacilityConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
