import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SubPreQualReportCategoriesTableComponent } from './sub-pre-qual-report-categories-table.component';

describe('SubPreQualReportCategoriesTableComponent', () => {
  let component: SubPreQualReportCategoriesTableComponent;
  let fixture: ComponentFixture<SubPreQualReportCategoriesTableComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportCategoriesTableComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportCategoriesTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
