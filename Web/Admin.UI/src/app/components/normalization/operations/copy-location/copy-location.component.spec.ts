import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CopyLocationComponent } from './copy-location.component';

describe('CopyLocationComponent', () => {
  let component: CopyLocationComponent;
  let fixture: ComponentFixture<CopyLocationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CopyLocationComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CopyLocationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
