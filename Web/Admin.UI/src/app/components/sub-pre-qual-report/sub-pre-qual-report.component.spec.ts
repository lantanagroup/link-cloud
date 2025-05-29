import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportComponent } from './sub-pre-qual-report.component';

describe('SubPreQualReportComponent', () => {
  let component: SubPreQualReportComponent;
  let fixture: ComponentFixture<SubPreQualReportComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
