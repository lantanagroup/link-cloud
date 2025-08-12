import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportIssuesTableComponent } from './sub-pre-qual-report-issues-table.component';

describe('SubPreQualReportIssuesTableComponent', () => {
  let component: SubPreQualReportIssuesTableComponent;
  let fixture: ComponentFixture<SubPreQualReportIssuesTableComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportIssuesTableComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportIssuesTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
