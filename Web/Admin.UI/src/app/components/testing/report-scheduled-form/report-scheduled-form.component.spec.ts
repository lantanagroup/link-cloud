import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReportScheduledFormComponent } from './report-scheduled-form.component';

describe('ReportScheduledFormComponent', () => {
  let component: ReportScheduledFormComponent;
  let fixture: ComponentFixture<ReportScheduledFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ReportScheduledFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReportScheduledFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
