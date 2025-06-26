import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CodeMapComponent } from './code-map.component';

describe('CodeMapComponent', () => {
  let component: CodeMapComponent;
  let fixture: ComponentFixture<CodeMapComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CodeMapComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CodeMapComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
