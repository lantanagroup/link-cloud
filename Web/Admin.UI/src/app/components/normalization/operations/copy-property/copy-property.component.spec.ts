import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CopyPropertyComponent } from './copy-property.component';

describe('CopyPropertyComponent', () => {
  let component: CopyPropertyComponent;
  let fixture: ComponentFixture<CopyPropertyComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CopyPropertyComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CopyPropertyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
