import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportSummaryComponent } from './sub-pre-qual-report-summary.component';

describe('SubPreQualReportSummaryComponent', () => {
  let component: SubPreQualReportSummaryComponent;
  let fixture: ComponentFixture<SubPreQualReportSummaryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportSummaryComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportSummaryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
