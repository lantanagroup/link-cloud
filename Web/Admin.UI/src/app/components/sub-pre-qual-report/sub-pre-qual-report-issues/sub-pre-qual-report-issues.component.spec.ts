import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportIssuesComponent } from './sub-pre-qual-report-issues.component';

describe('SubPreQualReportIssuesComponent', () => {
  let component: SubPreQualReportIssuesComponent;
  let fixture: ComponentFixture<SubPreQualReportIssuesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportIssuesComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportIssuesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
