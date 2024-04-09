import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ThemeShowcaseDialogComponent } from './theme-showcase-dialog.component';

describe('ThemeShowcaseDialogComponent', () => {
  let component: ThemeShowcaseDialogComponent;
  let fixture: ComponentFixture<ThemeShowcaseDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ ThemeShowcaseDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ThemeShowcaseDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
