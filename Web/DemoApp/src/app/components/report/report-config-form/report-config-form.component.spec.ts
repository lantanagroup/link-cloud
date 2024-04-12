import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReportConfigFormComponent } from './report-config-form.component';

describe('ReportConfigFormComponent', () => {
  let component: ReportConfigFormComponent;
  let fixture: ComponentFixture<ReportConfigFormComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ ReportConfigFormComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ReportConfigFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
