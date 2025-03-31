import { ComponentFixture, TestBed } from '@angular/core/testing';

import { NormalizationComponent } from './normalization.component';

describe('NormalizationComponent', () => {
  let component: NormalizationComponent;
  let fixture: ComponentFixture<NormalizationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NormalizationComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(NormalizationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
