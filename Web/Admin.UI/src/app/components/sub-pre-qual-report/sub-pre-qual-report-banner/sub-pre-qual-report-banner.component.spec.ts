import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportBannerComponent } from './sub-pre-qual-report-banner.component';

describe('SubPreQualReportBannerComponent', () => {
  let component: SubPreQualReportBannerComponent;
  let fixture: ComponentFixture<SubPreQualReportBannerComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportBannerComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
