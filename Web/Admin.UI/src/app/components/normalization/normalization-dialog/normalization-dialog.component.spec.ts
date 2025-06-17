import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NormalizationConfigDialogComponent } from './normalization-dialog.component';

describe('NormalizationDialogComponent', () => {
  let component: NormalizationConfigDialogComponent;
  let fixture: ComponentFixture<NormalizationConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NormalizationConfigDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NormalizationConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
