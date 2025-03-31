import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NormalizationDialogComponent } from './normalization-dialog.component';

describe('NormalizationDialogComponent', () => {
  let component: NormalizationDialogComponent;
  let fixture: ComponentFixture<NormalizationDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NormalizationDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NormalizationDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
