import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReportConfigDialogComponent } from './report-config-dialog.component';

describe('ReportConfigDialogComponent', () => {
  let component: ReportConfigDialogComponent;
  let fixture: ComponentFixture<ReportConfigDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReportConfigDialogComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReportConfigDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
