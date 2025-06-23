import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ConditionalTransformationComponent } from './conditional-transformation.component';

describe('ConditionalTransformationComponent', () => {
  let component: ConditionalTransformationComponent;
  let fixture: ComponentFixture<ConditionalTransformationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConditionalTransformationComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ConditionalTransformationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
