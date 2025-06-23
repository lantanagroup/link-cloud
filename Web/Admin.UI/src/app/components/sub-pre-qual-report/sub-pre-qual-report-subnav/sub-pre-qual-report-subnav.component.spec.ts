import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportSubnavComponent } from './sub-pre-qual-report-subnav.component';

describe('SubPreQualReportSubnavComponent', () => {
  let component: SubPreQualReportSubnavComponent;
  let fixture: ComponentFixture<SubPreQualReportSubnavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportSubnavComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportSubnavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
