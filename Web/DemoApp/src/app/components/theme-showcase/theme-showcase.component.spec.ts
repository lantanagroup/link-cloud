import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThemeShowcaseComponent } from './theme-showcase.component';

describe('ThemeShowcaseComponent', () => {
  let component: ThemeShowcaseComponent;
  let fixture: ComponentFixture<ThemeShowcaseComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ ThemeShowcaseComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ThemeShowcaseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
