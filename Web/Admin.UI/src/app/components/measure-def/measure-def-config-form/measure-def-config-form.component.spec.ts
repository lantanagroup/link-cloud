import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MeasureDefinitionFormComponent } from './measure-def-config-form.component';

describe('MeasureDefComponent', () => {
  let component: MeasureDefinitionFormComponent;
  let fixture: ComponentFixture<MeasureDefinitionFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [MeasureDefinitionFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MeasureDefinitionFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
