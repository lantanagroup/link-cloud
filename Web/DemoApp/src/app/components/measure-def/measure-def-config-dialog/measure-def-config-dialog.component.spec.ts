import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MeasureDefinitionDialogComponent } from './measure-def-config-dialog.component';

describe('MeasureDefinitionDialogComponent', () => {
  let component: MeasureDefinitionDialogComponent
  let fixture: ComponentFixture<MeasureDefinitionDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MeasureDefinitionDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MeasureDefinitionDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
