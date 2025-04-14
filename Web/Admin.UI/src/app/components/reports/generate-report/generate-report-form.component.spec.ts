import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GenerateReportComponentComponent } from './generate-report-component.component';

describe('GenerateReportComponentComponent', () => {
  let component: GenerateReportComponentComponent;
  let fixture: ComponentFixture<GenerateReportComponentComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GenerateReportComponentComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(GenerateReportComponentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
