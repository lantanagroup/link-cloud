import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NormalizationFormComponent } from './normalization.component';

describe('NormalizationComponent', () => {
  let component: NormalizationFormComponent;
  let fixture: ComponentFixture<NormalizationFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NormalizationFormComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NormalizationFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
