import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportMetaComponent } from './sub-pre-qual-report-meta.component';

describe('SubPreQualReportMetaComponent', () => {
  let component: SubPreQualReportMetaComponent;
  let fixture: ComponentFixture<SubPreQualReportMetaComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportMetaComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportMetaComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
